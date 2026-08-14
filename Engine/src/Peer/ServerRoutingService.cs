namespace BlueHeighliner.Comlink.Engine.Peer;

/// <summary>
/// Implements <see cref="IPeerService"/> for <see cref="NodeRole.Server"/>: listens on this server
/// user's configured endpoint for connections from its child clients, forms a long-term outbound
/// connection to every other server in the user map (retrying indefinitely on failure or disconnect),
/// and relays raw message bytes between them. A message received from a child client is routed to any
/// other local child it addresses and, once per remote server, forwarded to any other server that owns
/// an addressed child; a message received from another server is assumed already routed and is only
/// delivered to local children it addresses, never re-forwarded to other servers. Addressing operates on
/// the message's raw (unexpanded) address list — group expansion is not performed at the server. See
/// <c>Docs/Peer.md</c>.
/// </summary>
internal sealed class ServerRoutingService : IPeerService, IAsyncDisposable
{
    private static readonly TimeSpan DefaultRetryInterval = TimeSpan.FromSeconds(5);

    private readonly IOftHoster _hoster;
    private readonly IOftConnector _connector;
    private readonly INetworkTopology _networkTopology;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IOftCertificateProvider _certProvider;
    private readonly IMessageFormat _messageFormat;
    private readonly ILogger _logger;
    private readonly TimeSpan _retryInterval;

    private readonly ConcurrentDictionary<string, IOftConnection> _childConnections = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IOftConnection> _serverConnections = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<bool>> _inFlightSends = new();
    private IReadOnlyDictionary<string, ServerUserConfig> _userMap = new Dictionary<string, ServerUserConfig>();
    private IOftListener? _listener;

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

    /// <summary>Initializes a new <see cref="ServerRoutingService"/>.</summary>
    public ServerRoutingService(
        IOftHoster hoster,
        IOftConnector connector,
        INetworkTopology networkTopology,
        ICurrentUserProvider currentUserProvider,
        IOftCertificateProvider certProvider,
        IMessageFormat messageFormat,
        ILoggerFactory loggerFactory)
        : this(hoster, connector, networkTopology, currentUserProvider, certProvider, messageFormat, loggerFactory, DefaultRetryInterval)
    {
    }

    /// <summary>Initializes a new <see cref="ServerRoutingService"/> with a custom retry interval; intended for unit testing.</summary>
    internal ServerRoutingService(
        IOftHoster hoster,
        IOftConnector connector,
        INetworkTopology networkTopology,
        ICurrentUserProvider currentUserProvider,
        IOftCertificateProvider certProvider,
        IMessageFormat messageFormat,
        ILoggerFactory loggerFactory,
        TimeSpan retryInterval)
    {
        _hoster = hoster;
        _connector = connector;
        _networkTopology = networkTopology;
        _currentUserProvider = currentUserProvider;
        _certProvider = certProvider;
        _messageFormat = messageFormat;
        _logger = loggerFactory.CreateLogger("ACTIVITY");
        _retryInterval = retryInterval;
    }

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        _userMap = await _networkTopology.GetServerUsers(cancellation);
        string myName = _currentUserProvider.UserName ?? string.Empty;
        if (!_userMap.TryGetValue(myName, out ServerUserConfig? myConfig))
        {
            _logger.LogError("Server user {UserName} not found in the configured server user map; routing cannot start", myName);
            return;
        }

        _listener = await _hoster.Host(new IPEndPoint(IPAddress.Any, myConfig.Endpoint.Port), _certProvider.GetPeerOptions(), cancellation);
        _listener.ConnectedHandler = OnConnected;

        foreach ((string serverName, ServerUserConfig serverConfig) in _userMap)
        {
            if (string.Equals(serverName, myName, StringComparison.OrdinalIgnoreCase)) continue;
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
                connection = await _connector.Connect(endpoint.IpAddress, endpoint.Port, _certProvider.GetPeerOptions(), cancellation);
                _serverConnections[serverName] = connection;
                TaskCompletionSource disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ReceivedHandler = data => OnReceivedFromServer(serverName, data);
                connection.DisconnectedHandler = _ => disconnected.TrySetResult();
                _logger.LogInformation("Connected to server {ServerName}", serverName);
                await disconnected.Task.WaitAsync(cancellation);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Connection to server {ServerName} failed: {Message}", serverName, ex.Message);
            }
            finally
            {
                _serverConnections.TryRemove(serverName, out _);
                if (connection is not null) await connection.DisposeAsync();
            }

            try { await Task.Delay(_retryInterval, cancellation); }
            catch (OperationCanceledException) { return; }
        }
    }

    private void OnConnected(IOftConnection connection)
    {
        string remoteName = connection.Identity.Info;
        string myName = _currentUserProvider.UserName ?? string.Empty;

        bool isChild = _userMap.TryGetValue(myName, out ServerUserConfig? myConfig)
            && myConfig.ChildClients.Contains(remoteName, StringComparer.OrdinalIgnoreCase);
        bool isServer = !isChild && _userMap.ContainsKey(remoteName) && !string.Equals(remoteName, myName, StringComparison.OrdinalIgnoreCase);

        if (isChild)
        {
            _childConnections[remoteName] = connection;
            connection.ReceivedHandler = data => OnReceivedFromChild(remoteName, data);
            connection.DisconnectedHandler = ex => _childConnections.TryRemove(remoteName, out _);
            _logger.LogInformation("Child client {ClientName} connected", remoteName);
        }
        else if (isServer)
        {
            // Received-only: this server's own outbound leg (MaintainServerConnection) owns sending to,
            // and retrying the connection with, serverName — this inbound leg just supplies its traffic.
            connection.ReceivedHandler = data => OnReceivedFromServer(remoteName, data);
            _logger.LogInformation("Server {ServerName} connected inbound", remoteName);
        }
        else
        {
            _logger.LogWarning("Rejected connection from unrecognized identity {Identity}", remoteName);
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
        if (message is null) return;

        HashSet<string> addressedUsers = GetAddressedUsers(message);
        int priority = _messageFormat.GetPriority(message);
        string myName = _currentUserProvider.UserName ?? string.Empty;

        foreach (string addressedUser in addressedUsers)
        {
            if (_childConnections.TryGetValue(addressedUser, out IOftConnection? childConn))
                await TrySend(childConn, data, priority);
        }

        HashSet<string> targetServers = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string serverName, ServerUserConfig config) in _userMap)
        {
            if (string.Equals(serverName, myName, StringComparison.OrdinalIgnoreCase)) continue;
            if (config.ChildClients.Any(child => addressedUsers.Contains(child)))
                targetServers.Add(serverName);
        }
        foreach (string serverName in targetServers)
        {
            if (_serverConnections.TryGetValue(serverName, out IOftConnection? serverConn))
                await TrySend(serverConn, data, priority);
        }
    }

    private async Task HandleFromServer(string serverName, ReadOnlyMemory<byte> data)
    {
        object? message = TryDeserialize(data);
        if (message is null) return;

        HashSet<string> addressedUsers = GetAddressedUsers(message);
        int priority = _messageFormat.GetPriority(message);

        foreach (string addressedUser in addressedUsers)
        {
            if (_childConnections.TryGetValue(addressedUser, out IOftConnection? childConn))
                await TrySend(childConn, data, priority);
        }
    }

    private HashSet<string> GetAddressedUsers(object message) =>
        new(_messageFormat.GetAddresses(message).Select(a => a.UserName), StringComparer.OrdinalIgnoreCase);

    private object? TryDeserialize(ReadOnlyMemory<byte> data)
    {
        try { return PeerSerializer.Deserialize(_messageFormat.MessageType, data); }
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
        string messageId = _messageFormat.GetMessageId(message);
        return _inFlightSends.GetOrAdd(messageId, _ => SendOnceAndCleanup(messageId, message, cancellation));
    }

    private async Task<bool> SendOnceAndCleanup(string messageId, object message, CancellationToken cancellation)
    {
        try
        {
            using OwnedBuffer buf = PeerSerializer.Serialize(message);
            await HandleFromChild(_currentUserProvider.UserName ?? string.Empty, buf.Memory);
            return true;
        }
        finally
        {
            _inFlightSends.TryRemove(messageId, out _);
        }
    }

    /// <inheritdoc />
    public async Task DeliverLocal(object payload)
    {
        _logger.LogInformation("{MessageId} delivered locally from {FromUser}", _messageFormat.GetMessageId(payload), _messageFormat.GetFromUser(payload));
        if (MessageDelivered is not null)
            await MessageDelivered(payload);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _listener?.Dispose();
        foreach (IOftConnection connection in _childConnections.Values)
            await connection.DisposeAsync();
        foreach (IOftConnection connection in _serverConnections.Values)
            await connection.DisposeAsync();
    }
}
