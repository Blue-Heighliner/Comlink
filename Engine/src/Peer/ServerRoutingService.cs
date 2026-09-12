namespace BlueHeighliner.Comlink.Engine.Peer;

/// <summary>
/// Implements <see cref="IPeerService"/> for <see cref="NodeRole.Server"/>: listens on this server user's
/// configured endpoint for connections from its child clients and other servers, and relays raw message
/// bytes between them by dialing out to each recipient's own endpoint - MSMT's client-request/server-
/// response model means a connection a remote peer initiated can only ever be used to acknowledge what
/// that peer sends, never to push something new back down it, so delivering to any recipient (a child or
/// another server) always means this instance acting as an MSMT client and connecting out to that
/// recipient's own receiver, identified by certificate subject rather than any self-declared name. A
/// message received from a child client is routed to any other local child it addresses and, once per
/// remote server, forwarded to any other server that owns an addressed child; a message received from
/// another server is assumed already routed and is only delivered to local children it addresses, never
/// re-forwarded to other servers. Addressing operates on the message's raw (unexpanded) address list —
/// group expansion is not performed at the server. Also implements <see cref="IConnectionStatusService"/>,
/// tracking connect/disconnect status and timestamps for every own child client and every other server in
/// the cluster. A background <see cref="MsmtConnectionMonitor"/> per child and per sibling server
/// proactively opens and maintains that connection with a recurring heartbeat, independent of whether any
/// real message is actually being routed, so <see cref="GetStatuses"/> reflects each connection's live state
/// continuously rather than only the moment a message last happened to flow. See <c>Docs/Peer.md</c>.
/// </summary>
internal sealed class ServerRoutingService : IPeerService, IConnectionStatusService, IAsyncDisposable
{
    /// <summary>Initializes a new <see cref="ServerRoutingService"/>.</summary>
    public ServerRoutingService(
        IMsmtPeerFactory peerFactory,
        IEngineController engineController,
        ICurrentUserProvider currentUserProvider,
        ILoggerFactory loggerFactory)
    {
        this.peerFactory = peerFactory;
        this.engineController = engineController;
        this.currentUserProvider = currentUserProvider;
        logger = loggerFactory.CreateLogger("ACTIVITY");
    }

    private readonly IMsmtPeerFactory peerFactory;
    private readonly IEngineController engineController;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly ILogger logger;
    private readonly MsmtConnectionMonitor connectionMonitor = new();

    private readonly ConcurrentDictionary<string, bool> childConnected = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> serverConnected = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<bool>> inFlightSends = new();
    private readonly ConcurrentDictionary<string, DateTime> serverLastConnectedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> serverLastDisconnectedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> childLastConnectedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> childLastDisconnectedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<IMsmtConnection, string> inboundConnectionNames = new();
    private IReadOnlyDictionary<string, ServerUserConfig> userMap = new Dictionary<string, ServerUserConfig>();
    private IMsmtPeer? peer;

    /// <inheritdoc />
    public event Func<object, Task>? MessageDelivered;
#pragma warning disable CS0067 // A server relays raw message bytes without deserializing for confirmation-vs-normal classification, so this never fires.
    /// <inheritdoc />
    public event Func<string, string, Task>? ConfirmationReceived;
#pragma warning restore CS0067
#pragma warning disable CS0067 // No per-message delivery status is tracked across the client/server hierarchy.
    /// <inheritdoc />
    public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;
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

        MsmtOptions options;
        try
        {
            options = engineController.ConnectionOptions;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError("Server role cannot start: {Message}", ex.Message);
            return;
        }

        peer = peerFactory.Create(options);
        peer.Connected.Subscribe(OnConnected);
        peer.Disconnected.Subscribe(OnDisconnected);
        peer.Received.Subscribe(OnReceived);
        peer.StartListener(myConfig.Endpoint.Port);
        StartMonitoring(peer, myName, myConfig, cancellation);

        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
    }

    private void StartMonitoring(IMsmtPeer activePeer, string myName, ServerUserConfig myConfig, CancellationToken cancellation)
    {
        foreach (string childName in myConfig.ChildClients)
        {
            UserEndpoint? endpoint = engineController.GetEndpoint(childName);
            if (endpoint is null)
            {
                logger.LogWarning("Cannot monitor child {ChildName}: no endpoint configured for it (add it to the Users map)", childName);
                continue;
            }

            MsmtTarget target = new() { Host = endpoint.IpAddress, Port = endpoint.Port };
            connectionMonitor.Maintain(activePeer, target, cancellation);
        }

        foreach ((string serverName, ServerUserConfig config) in userMap)
        {
            if (string.Equals(serverName, myName, StringComparison.OrdinalIgnoreCase)) { continue; }

            MsmtTarget target = new() { Host = config.Endpoint.IpAddress, Port = config.Endpoint.Port };
            connectionMonitor.Maintain(activePeer, target, cancellation);
        }
    }

    private void UpdateChildStatus(string childName, bool connected)
    {
        bool changed = childConnected.GetValueOrDefault(childName) != connected;
        if (!changed) { return; }

        if (connected)
        {
            childConnected[childName] = true;
            childLastConnectedAt[childName] = DateTime.UtcNow;
            logger.LogInformation("Connected to child client {ClientName}", childName);
        }
        else
        {
            childConnected.TryRemove(childName, out _);
            childLastDisconnectedAt[childName] = DateTime.UtcNow;
            logger.LogWarning("Child client {ClientName} unreachable", childName);
        }
        StatusesChanged?.Invoke();
    }

    private void UpdateServerStatus(string serverName, bool connected)
    {
        bool changed = serverConnected.GetValueOrDefault(serverName) != connected;
        if (!changed) { return; }

        if (connected)
        {
            serverConnected[serverName] = true;
            serverLastConnectedAt[serverName] = DateTime.UtcNow;
            logger.LogInformation("Connected to server {ServerName}", serverName);
        }
        else
        {
            serverConnected.TryRemove(serverName, out _);
            serverLastDisconnectedAt[serverName] = DateTime.UtcNow;
            logger.LogWarning("Server {ServerName} unreachable", serverName);
        }
        StatusesChanged?.Invoke();
    }

    private void OnConnected(MsmtConnectedEventArgs args)
    {
        if (args.Connection.Sender is not null)
        {
            string? serverName = FindNameByTarget(userMap.Keys, args.Connection.Target);
            if (serverName is not null)
            {
                UpdateServerStatus(serverName, true);
                return;
            }

            string? childName = FindChildNameByTarget(args.Connection.Target);
            if (childName is not null)
            {
                UpdateChildStatus(childName, true);
            }
            return;
        }

        string myName = currentUserProvider.UserName ?? string.Empty;
        IReadOnlyList<string> childNames = userMap.TryGetValue(myName, out ServerUserConfig? myConfig) ? myConfig.ChildClients : [];

        string? subject = args.Connection.Identity?.Subject;
        string? remoteName = subject is null ? null
            : FindNameByCertificateSubject(childNames, subject)
                ?? FindNameByCertificateSubject(userMap.Keys.Where(name => !string.Equals(name, myName, StringComparison.OrdinalIgnoreCase)), subject);

        if (remoteName is null)
        {
            logger.LogWarning("Rejected connection from unrecognized certificate {Subject}", subject);
            args.Connection.Drop();
            return;
        }

        inboundConnectionNames[args.Connection] = remoteName;

        if (childNames.Contains(remoteName, StringComparer.OrdinalIgnoreCase))
        {
            UpdateChildStatus(remoteName, true);
        }
        else
        {
            UpdateServerStatus(remoteName, true);
        }
    }

    private void OnDisconnected(MsmtDisconnectedEventArgs args)
    {
        if (!inboundConnectionNames.TryRemove(args.Connection, out string? remoteName)) { return; }

        string myName = currentUserProvider.UserName ?? string.Empty;
        IReadOnlyList<string> childNames = userMap.TryGetValue(myName, out ServerUserConfig? myConfig) ? myConfig.ChildClients : [];

        if (childNames.Contains(remoteName, StringComparer.OrdinalIgnoreCase))
        {
            UpdateChildStatus(remoteName, false);
        }
        else
        {
            UpdateServerStatus(remoteName, false);
        }
    }

    private string? FindNameByTarget(IEnumerable<string> candidates, MsmtTarget target)
        => candidates.FirstOrDefault(name =>
            userMap.TryGetValue(name, out ServerUserConfig? config)
            && config.Endpoint.IpAddress == target.Host
            && config.Endpoint.Port == target.Port);

    private string? FindChildNameByTarget(MsmtTarget target)
    {
        string myName = currentUserProvider.UserName ?? string.Empty;
        IReadOnlyList<string> childNames = userMap.TryGetValue(myName, out ServerUserConfig? myConfig) ? myConfig.ChildClients : [];
        return childNames.FirstOrDefault(name =>
            engineController.GetEndpoint(name) is { } endpoint
            && endpoint.IpAddress == target.Host
            && endpoint.Port == target.Port);
    }

    private string? FindNameByCertificateSubject(IEnumerable<string> candidates, string subject)
    {
        string simpleName = ExtractCommonName(subject);
        return candidates.FirstOrDefault(name => string.Equals(engineController.GetCertificateName(name), simpleName, StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractCommonName(string distinguishedName)
    {
        foreach (string component in distinguishedName.Split(','))
        {
            string trimmed = component.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed["CN=".Length..];
            }
        }
        return distinguishedName;
    }

    private void OnReceived(MsmtReceivedEventArgs args)
    {
        if (!inboundConnectionNames.TryGetValue(args.Link.Connection, out string? remoteName)) { return; }

        byte[] copy;
        using (args.Payload) { copy = args.Payload.Memory.ToArray(); }

        // An empty payload is a MsmtConnectionMonitor heartbeat, not a real message to relay.
        if (copy.Length == 0) { return; }

        string myName = currentUserProvider.UserName ?? string.Empty;
        IReadOnlyList<string> childNames = userMap.TryGetValue(myName, out ServerUserConfig? myConfig) ? myConfig.ChildClients : [];

        if (childNames.Contains(remoteName, StringComparer.OrdinalIgnoreCase))
        {
            _ = Task.Run(() => HandleFromChild(remoteName, copy));
        }
        else
        {
            _ = Task.Run(() => HandleFromServer(remoteName, copy));
        }
    }

    private async Task HandleFromChild(string childName, ReadOnlyMemory<byte> data)
    {
        object? message = TryDeserialize(data);
        if (message is null) { return; }

        HashSet<string> addressedUsers = GetAddressedUsers(message);
        int priority = engineController.GetPriority(message);
        string myName = currentUserProvider.UserName ?? string.Empty;

        if (!userMap.TryGetValue(myName, out ServerUserConfig? myConfig)) { return; }

        foreach (string addressedUser in addressedUsers)
        {
            if (myConfig.ChildClients.Contains(addressedUser, StringComparer.OrdinalIgnoreCase))
            {
                await TrySendToChild(addressedUser, data);
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
            await TrySendToServer(serverName, data);
        }
    }

    private async Task HandleFromServer(string serverName, ReadOnlyMemory<byte> data)
    {
        object? message = TryDeserialize(data);
        if (message is null) { return; }

        HashSet<string> addressedUsers = GetAddressedUsers(message);
        string myName = currentUserProvider.UserName ?? string.Empty;

        if (!userMap.TryGetValue(myName, out ServerUserConfig? myConfig)) { return; }

        foreach (string addressedUser in addressedUsers)
        {
            if (myConfig.ChildClients.Contains(addressedUser, StringComparer.OrdinalIgnoreCase))
            {
                await TrySendToChild(addressedUser, data);
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

    private async Task TrySendToChild(string childName, ReadOnlyMemory<byte> data)
    {
        if (peer is null) { return; }
        UserEndpoint? endpoint = engineController.GetEndpoint(childName);
        if (endpoint is null)
        {
            logger.LogWarning("Cannot deliver to child {ChildName}: no endpoint configured for it (add it to the Users map)", childName);
            return;
        }

        try
        {
            MsmtResponse response = await peer.Request(new MsmtTarget { Host = endpoint.IpAddress, Port = endpoint.Port }, data, cancellation: CancellationToken.None);
            response.Payload.Dispose();
        }
        catch { }
    }

    private async Task TrySendToServer(string serverName, ReadOnlyMemory<byte> data)
    {
        if (peer is null) { return; }
        if (!userMap.TryGetValue(serverName, out ServerUserConfig? config)) { return; }

        try
        {
            MsmtResponse response = await peer.Request(new MsmtTarget { Host = config.Endpoint.IpAddress, Port = config.Endpoint.Port }, data, cancellation: CancellationToken.None);
            response.Payload.Dispose();
        }
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
                    IsConnected = childConnected.ContainsKey(childName),
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
                IsConnected = serverConnected.ContainsKey(serverName),
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
        if (peer is not null) { await peer.DisposeAsync(); }
    }
}
