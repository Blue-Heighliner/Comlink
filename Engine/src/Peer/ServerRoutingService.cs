namespace BlueHeighliner.Comlink.Engine.Peer;

/// <summary>
/// Implements <see cref="IPeerService"/> for <see cref="NodeRole.Server"/>: listens on this server
/// user's configured endpoint for connections from its child clients, forms a long-term outbound
/// connection to every other server in the user map (retrying indefinitely on failure or disconnect),
/// and relays raw message bytes between them. A message received from a child client is routed to any
/// other local child it addresses and, once per remote server, forwarded to any other server that owns
/// an addressed child; a message received from another server is assumed already routed and is only
/// delivered to local children it addresses, never re-forwarded to other servers. Addressing operates on
/// the message's raw (unexpanded) address list — group expansion is not performed at the server. Also
/// implements <see cref="IConnectionStatusService"/>, tracking connect/disconnect status and timestamps
/// for every own child client and every other server in the cluster. See <c>Docs/Peer.md</c>.
/// </summary>
internal sealed class ServerRoutingService : IPeerService, IConnectionStatusService, IAsyncDisposable
{
    private static readonly TimeSpan defaultRetryInterval = TimeSpan.FromSeconds(5);

    /// <summary>Initializes a new <see cref="ServerRoutingService"/>.</summary>
    public ServerRoutingService(
        IOftHoster hoster,
        IOftConnector connector,
        IEngineController engineController,
        ICurrentUserProvider currentUserProvider,
        ILoggerFactory loggerFactory)
        : this(hoster, connector, engineController, currentUserProvider, loggerFactory, defaultRetryInterval)
    {
    }

    /// <summary>Initializes a new <see cref="ServerRoutingService"/> with a custom retry interval; intended for unit testing.</summary>
    internal ServerRoutingService(
        IOftHoster hoster,
        IOftConnector connector,
        IEngineController engineController,
        ICurrentUserProvider currentUserProvider,
        ILoggerFactory loggerFactory,
        TimeSpan retryInterval)
    {
        this.hoster = hoster;
        this.connector = connector;
        this.engineController = engineController;
        this.currentUserProvider = currentUserProvider;
        logger = loggerFactory.CreateLogger("ACTIVITY");
        this.retryInterval = retryInterval;
    }

    private readonly IOftHoster hoster;
    private readonly IOftConnector connector;
    private readonly IEngineController engineController;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly ILogger logger;
    private readonly TimeSpan retryInterval;

    private readonly ConcurrentDictionary<string, IOftConnection> childConnections = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IOftConnection> serverConnections = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<bool>> inFlightSends = new();
    private readonly ConcurrentDictionary<string, DateTime> serverLastConnectedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> serverLastDisconnectedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> childLastConnectedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> childLastDisconnectedAt = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, ServerUserConfig> userMap = new Dictionary<string, ServerUserConfig>();
    private IOftListener? listener;

    /// <inheritdoc />
    public event Func<object, Task>? MessageDelivered;
#pragma warning disable CS0067 // A server relays raw message bytes without deserializing for confirmation-vs-normal classification, so this never fires.
    /// <inheritdoc />
    public event Func<string, string, Task>? ConfirmationReceived;
#pragma warning restore CS0067
#pragma warning disable CS0067 // No per-message OFT delivery status is tracked across the client/server hierarchy.
    /// <inheritdoc />
    public event Func<string, string, OftDeliveryStatus, Task>? DeliveryStatusChanged;
#pragma warning restore CS0067
    /// <inheritdoc />
    public event Action? StatusesChanged;

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        userMap = engineController.Servers;
        string myName = currentUserProvider.UserName ?? string.Empty;
        if (!userMap.TryGetValue(myName, out ServerUserConfig? myConfig))
        {
            logger.LogError("Server user {UserName} not found in the configured server user map; routing cannot start", myName);
            return;
        }

        listener = await hoster.Host(new IPEndPoint(IPAddress.Any, myConfig.Endpoint.Port), engineController.ConnectionOptions, cancellation);
        listener.ConnectedHandler = OnConnected;

        foreach ((string serverName, ServerUserConfig serverConfig) in userMap)
        {
            if (string.Equals(serverName, myName, StringComparison.OrdinalIgnoreCase)) { continue; }
            _ = MaintainServerConnection(serverName, serverConfig.Endpoint, cancellation);
        }

        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
    }

    private async Task MaintainServerConnection(string serverName, UserEndpoint endpoint, CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            IOftConnection? connection = null;
            try
            {
                connection = await connector.Connect(endpoint.IpAddress, endpoint.Port, engineController.ConnectionOptions, cancellation);
                serverConnections[serverName] = connection;
                serverLastConnectedAt[serverName] = DateTime.UtcNow;
                StatusesChanged?.Invoke();
                TaskCompletionSource disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ReceivedHandler = data => OnReceivedFromServer(serverName, data);
                connection.DisconnectedHandler = _ => disconnected.TrySetResult();
                logger.LogInformation("Connected to server {ServerName}", serverName);
                await disconnected.Task.WaitAsync(cancellation);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Connection to server {ServerName} failed: {Message}", serverName, ex.Message);
            }
            finally
            {
                serverConnections.TryRemove(serverName, out _);
                if (connection is not null)
                {
                    serverLastDisconnectedAt[serverName] = DateTime.UtcNow;
                    StatusesChanged?.Invoke();
                    await connection.DisposeAsync();
                }
            }

            try { await Task.Delay(retryInterval, cancellation); }
            catch (OperationCanceledException) { return; }
        }
    }

    private void OnConnected(IOftConnection connection)
    {
        string remoteName = connection.Identity.Info;
        string myName = currentUserProvider.UserName ?? string.Empty;

        bool isChild = userMap.TryGetValue(myName, out ServerUserConfig? myConfig)
            && myConfig.ChildClients.Contains(remoteName, StringComparer.OrdinalIgnoreCase);
        bool isServer = !isChild && userMap.ContainsKey(remoteName) && !string.Equals(remoteName, myName, StringComparison.OrdinalIgnoreCase);

        if (isChild)
        {
            childConnections[remoteName] = connection;
            childLastConnectedAt[remoteName] = DateTime.UtcNow;
            StatusesChanged?.Invoke();
            connection.ReceivedHandler = data => OnReceivedFromChild(remoteName, data);
            connection.DisconnectedHandler = ex =>
            {
                childConnections.TryRemove(remoteName, out _);
                childLastDisconnectedAt[remoteName] = DateTime.UtcNow;
                StatusesChanged?.Invoke();
            };
            logger.LogInformation("Child client {ClientName} connected", remoteName);
        }
        else if (isServer)
        {
            // Received-only: this server's own outbound leg (MaintainServerConnection) owns sending to,
            // and retrying the connection with, serverName — this inbound leg just supplies its traffic.
            connection.ReceivedHandler = data => OnReceivedFromServer(remoteName, data);
            logger.LogInformation("Server {ServerName} connected inbound", remoteName);
        }
        else
        {
            logger.LogWarning("Rejected connection from unrecognized identity {Identity}", remoteName);
            _ = connection.DisposeAsync();
        }
    }

    private void OnReceivedFromChild(string childName, IMemoryOwner<byte> data)
    {
        byte[] copy;
        using (data) { copy = data.Memory.ToArray(); }
        _ = Task.Run(() => HandleFromChild(childName, copy));
    }

    private void OnReceivedFromServer(string serverName, IMemoryOwner<byte> data)
    {
        byte[] copy;
        using (data) { copy = data.Memory.ToArray(); }
        _ = Task.Run(() => HandleFromServer(serverName, copy));
    }

    private async Task HandleFromChild(string childName, ReadOnlyMemory<byte> data)
    {
        object? message = TryDeserialize(data);
        if (message is null) { return; }

        HashSet<string> addressedUsers = GetAddressedUsers(message);
        int priority = engineController.GetPriority(message);
        string myName = currentUserProvider.UserName ?? string.Empty;

        foreach (string addressedUser in addressedUsers)
        {
            if (childConnections.TryGetValue(addressedUser, out IOftConnection? childConn))
            {
                await TrySend(childConn, data, priority);
            }
        }

        HashSet<string> targetServers = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string serverName, ServerUserConfig config) in userMap)
        {
            if (string.Equals(serverName, myName, StringComparison.OrdinalIgnoreCase)) { continue; }
            if (config.ChildClients.Any(child => addressedUsers.Contains(child)))
            {
                targetServers.Add(serverName);
            }
        }
        foreach (string serverName in targetServers)
        {
            if (serverConnections.TryGetValue(serverName, out IOftConnection? serverConn))
            {
                await TrySend(serverConn, data, priority);
            }
        }
    }

    private async Task HandleFromServer(string serverName, ReadOnlyMemory<byte> data)
    {
        object? message = TryDeserialize(data);
        if (message is null) { return; }

        HashSet<string> addressedUsers = GetAddressedUsers(message);
        int priority = engineController.GetPriority(message);

        foreach (string addressedUser in addressedUsers)
        {
            if (childConnections.TryGetValue(addressedUser, out IOftConnection? childConn))
            {
                await TrySend(childConn, data, priority);
            }
        }
    }

    private HashSet<string> GetAddressedUsers(object message)
        => new(engineController.GetAddresses(message).Select(a => a.UserName), StringComparer.OrdinalIgnoreCase);

    private object? TryDeserialize(ReadOnlyMemory<byte> data)
    {
        try { return PeerSerializer.Deserialize(engineController.MessageType, data); }
        catch { return null; }
    }

    private static async Task TrySend(IOftConnection connection, ReadOnlyMemory<byte> data, int priority)
    {
        try { await connection.Send(data, priority); }
        catch { }
    }

    /// <inheritdoc />
    public Task<bool> Send(string userName, object message, CancellationToken cancellation = default)
    {
        string messageId = engineController.GetMessageId(message);
        return inFlightSends.GetOrAdd(messageId, _ => SendOnceAndCleanup(messageId, message, cancellation));
    }

    /// <inheritdoc />
    public IReadOnlyList<PeerConnectionStatus> GetStatuses()
    {
        string myName = currentUserProvider.UserName ?? string.Empty;
        List<PeerConnectionStatus> statuses = [];

        if (userMap.TryGetValue(myName, out ServerUserConfig? myConfig))
        {
            foreach (string childName in myConfig.ChildClients)
            {
                statuses.Add(new PeerConnectionStatus
                {
                    UserName = childName,
                    Kind = PeerConnectionKind.Client,
                    IsConnected = childConnections.ContainsKey(childName),
                    LastConnectedAt = childLastConnectedAt.TryGetValue(childName, out DateTime connectedAt) ? connectedAt : null,
                    LastDisconnectedAt = childLastDisconnectedAt.TryGetValue(childName, out DateTime disconnectedAt) ? disconnectedAt : null
                });
            }
        }

        foreach (string serverName in userMap.Keys)
        {
            if (string.Equals(serverName, myName, StringComparison.OrdinalIgnoreCase)) { continue; }
            statuses.Add(new PeerConnectionStatus
            {
                UserName = serverName,
                Kind = PeerConnectionKind.Server,
                IsConnected = serverConnections.ContainsKey(serverName),
                LastConnectedAt = serverLastConnectedAt.TryGetValue(serverName, out DateTime connectedAt) ? connectedAt : null,
                LastDisconnectedAt = serverLastDisconnectedAt.TryGetValue(serverName, out DateTime disconnectedAt) ? disconnectedAt : null
            });
        }

        return statuses;
    }

    private async Task<bool> SendOnceAndCleanup(string messageId, object message, CancellationToken cancellation)
    {
        try
        {
            using OwnedBuffer buf = PeerSerializer.Serialize(message);
            await HandleFromChild(currentUserProvider.UserName ?? string.Empty, buf.Memory);
            return true;
        }
        finally
        {
            inFlightSends.TryRemove(messageId, out _);
        }
    }

    /// <inheritdoc />
    public async Task DeliverLocal(object payload)
    {
        logger.LogInformation("{MessageId} delivered locally from {FromUser}", engineController.GetMessageId(payload), engineController.GetFromUser(payload));
        if (MessageDelivered is not null)
        {
            await MessageDelivered(payload);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        listener?.Dispose();
        foreach (IOftConnection connection in childConnections.Values)
        {
            await connection.DisposeAsync();
        }
        foreach (IOftConnection connection in serverConnections.Values)
        {
            await connection.DisposeAsync();
        }
    }
}
