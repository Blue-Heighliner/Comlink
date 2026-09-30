namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// Implements <see cref="IPeerService"/> for <see cref="NodeRole.Server"/>: listens on <see cref="IEngineController.PeerPort"/>
/// for connections from its child clients and other servers, keeps a connection open to each of its
/// <see cref="IEngineController.OutgoingPoints"/>, and relays raw message bytes between them. Nothing is configured
/// about where a child or another server is: a connection is matched to one by the identity it is given when it forms
/// (see <see cref="IdentifyingPeerTransport"/>), a connection whose identity is neither a child nor a server in the
/// cluster is dropped, and connections are bidirectional, so a message for a user goes back over whichever connection is
/// identified as them, whichever end opened it. A
/// message received from a child client is routed to any other local child it addresses and, once per
/// remote server, forwarded to any other server that owns an addressed child; a message received from
/// another server is assumed already routed and is only delivered to local children it addresses, never
/// re-forwarded to other servers. Addressing operates on the message's raw (unexpanded) address list -
/// group expansion is not performed at the server. Also implements <see cref="IConnectionStatusService"/>,
/// tracking connect/disconnect status and timestamps for every own child client and every other server in
/// the cluster. A background <see cref="PeerConnectionMonitor"/> per outgoing point
/// proactively opens and maintains that connection with a recurring heartbeat, independent of whether any
/// real message is actually being routed, so <see cref="GetStatuses"/> reflects each connection's live state
/// continuously rather than only the moment a message last happened to flow. See <c>Docs/Components/Peer.md</c>.
/// </summary>
internal sealed class ServerRoutingService : IPeerService, IConnectionStatusService, IAsyncDisposable
{
    /// <summary>Initializes a new <see cref="ServerRoutingService"/>.</summary>
    public ServerRoutingService(
        IPeerTransportFactory transportFactory,
        IEngineController engineController,
        ICurrentUserProvider currentUserProvider,
        IMessageStorageService storage,
        ILoggerFactory loggerFactory)
    {
        this.transportFactory = transportFactory;
        this.engineController = engineController;
        this.currentUserProvider = currentUserProvider;
        this.storage = storage;
        logger = loggerFactory.CreateLogger("ACTIVITY");
    }

    private readonly IPeerTransportFactory transportFactory;
    private readonly IEngineController engineController;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly IMessageStorageService storage;
    private readonly ILogger logger;
    private readonly PeerConnectionMonitor connectionMonitor = new();

    private readonly ConcurrentDictionary<string, bool> childConnected = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> serverConnected = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<bool>> inFlightSends = new();
    private readonly ConcurrentDictionary<string, DateTime> serverLastConnectedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> serverLastDisconnectedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> childLastConnectedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> childLastDisconnectedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly UserConnections connections = new();
    private readonly Lock statusLock = new();
    private readonly ConcurrentDictionary<string, bool> closedNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConnectionPoint> points = new();
    private readonly ConcurrentDictionary<string, PeerLinkControl> pointMonitors = new();
    private readonly ConcurrentDictionary<string, string> pointUsers = new();
    private IReadOnlyDictionary<string, ServerUserConfig> userMap = new Dictionary<string, ServerUserConfig>();
    private IPeerTransport? transport;
    private int disposed;

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
    public event Func<string, Task>? UserConnected;
    /// <inheritdoc />
    public event Func<string, Task>? UserDisconnected;

    /// <inheritdoc />
    public IReadOnlyList<string> GetConnectedUsers() => [.. childConnected.Keys, .. serverConnected.Keys];

    /// <inheritdoc />
    public bool IsUserConnected(string userName) => childConnected.ContainsKey(userName) || serverConnected.ContainsKey(userName);

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        userMap = engineController.Servers;
        string myName = currentUserProvider.UserName ?? string.Empty;
        if (!userMap.ContainsKey(myName))
        {
            logger.LogError("Server user {UserName} not found in the configured server user map; routing cannot start", myName);
            return;
        }

        transport = transportFactory.Create();
        transport.Connected.Listen(OnConnected);
        transport.Disconnected.Listen(OnDisconnected);
        transport.Received.Listen(OnReceived);
        transport.StartListener(engineController.PeerPort);
        foreach (ConnectionPoint point in engineController.OutgoingPoints)
        {
            points[point.Key] = point;
            pointMonitors[point.Key] = connectionMonitor.Maintain(transport, point, cancellation, OnHeartbeatAcknowledged);
        }

        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
    }

    private IReadOnlyList<string> GetChildNames()
        => userMap.TryGetValue(currentUserProvider.UserName ?? string.Empty, out ServerUserConfig? myConfig) ? myConfig.ChildClients : [];

    private string? FindChild(string name) => GetChildNames().FirstOrDefault(child => string.Equals(child, name, StringComparison.OrdinalIgnoreCase));

    private string? FindSiblingServer(string name)
        => string.Equals(name, currentUserProvider.UserName, StringComparison.OrdinalIgnoreCase)
            ? null
            : userMap.Keys.FirstOrDefault(server => string.Equals(server, name, StringComparison.OrdinalIgnoreCase));

    private bool IsChild(string name) => FindChild(name) is not null;

    private bool IsSiblingServer(string name) => FindSiblingServer(name) is not null;

    private void UpdateChildStatus(string childName, bool connected)
    {
        lock (statusLock)
        {
            if (childConnected.GetValueOrDefault(childName) == connected) { return; }

            if (connected)
            {
                childConnected[childName] = true;
                childLastConnectedAt[childName] = DateTime.UtcNow;
            }
            else
            {
                childConnected.TryRemove(childName, out _);
                childLastDisconnectedAt[childName] = DateTime.UtcNow;
            }
        }

        if (connected) { logger.LogInformation("Connected to child client {ClientName}", childName); }
        else { logger.LogWarning("Child client {ClientName} unreachable", childName); }
        StatusesChanged?.Invoke();
        PeerConnectionNotifier.Raise(connected ? UserConnected : UserDisconnected, childName, connected ? "connecting" : "disconnecting", logger);
    }

    private void UpdateServerStatus(string serverName, bool connected)
    {
        lock (statusLock)
        {
            if (serverConnected.GetValueOrDefault(serverName) == connected) { return; }

            if (connected)
            {
                serverConnected[serverName] = true;
                serverLastConnectedAt[serverName] = DateTime.UtcNow;
            }
            else
            {
                serverConnected.TryRemove(serverName, out _);
                serverLastDisconnectedAt[serverName] = DateTime.UtcNow;
            }
        }

        if (connected) { logger.LogInformation("Connected to server {ServerName}", serverName); }
        else { logger.LogWarning("Server {ServerName} unreachable", serverName); }
        StatusesChanged?.Invoke();
        PeerConnectionNotifier.Raise(connected ? UserConnected : UserDisconnected, serverName, connected ? "connecting" : "disconnecting", logger);
    }

    private void OnConnected(PeerConnectionEventArgs args)
    {
        PeerConnection connection = args.Connection;
        string name = connection.User?.Name ?? string.Empty;
        if (!IsChild(name) && !IsSiblingServer(name))
        {
            logger.LogWarning("Rejected connection from {Name}, which is neither a child client nor another server in the cluster", name);
            connection.Drop();
            return;
        }

        if (closedNames.ContainsKey(name))
        {
            connection.Drop();
            return;
        }

        connections.Add(connection);
        if (connection.Point is { } point) { pointUsers[point.Key] = name; }

        // An IP connection this node dialed only counts as up once a heartbeat is acknowledged (see
        // OnHeartbeatAcknowledged): a node that has closed the connection accepts it and drops it again without
        // answering, so counting the bare connection would flash the row green every time. A connection the other
        // node opened, or a serial link (which only comes up when the far end answers), is up straight away.
        if (connection.IsInbound || connection.IsSerial) { UpdateStatusForName(name, true); }
    }

    private void OnDisconnected(PeerConnectionEventArgs args)
    {
        if (connections.Remove(args.Connection) is not { } name) { return; }

        // A user can be connected both ways at once (it dialed this node, and this node dialed it); losing one of
        // them does not make it unreachable while the other is still up.
        if (connections.Has(name)) { return; }

        UpdateStatusForName(name, false);

        // An unexpected drop (as opposed to this node's own Close/Refresh action, which already wakes the
        // monitor itself) would otherwise sit unnoticed until the monitor's current heartbeat interval elapses -
        // up to steadyInterval - since nothing else wakes a sleeping monitor. Waking it here lets it retry (and
        // report the reconnect) right away instead.
        foreach (string key in GetPointKeys(name))
        {
            if (pointMonitors.TryGetValue(key, out PeerLinkControl? monitor)) { monitor.NotifyLost(); }
        }
    }

    private List<string> GetPointKeys(string userName)
        => [.. pointUsers.Where(entry => string.Equals(entry.Value, userName, StringComparison.OrdinalIgnoreCase)).Select(entry => entry.Key)];

    private void OnHeartbeatAcknowledged(PeerConnection connection)
    {
        if (!connections.Contains(connection) || connection.User is not { } user) { return; }
        if (!closedNames.ContainsKey(user.Name)) { UpdateStatusForName(user.Name, true); }
    }

    private void UpdateStatusForName(string remoteName, bool isConnected)
    {
        if (FindChild(remoteName) is { } child)
        {
            UpdateChildStatus(child, isConnected);
        }
        else
        {
            UpdateServerStatus(FindSiblingServer(remoteName) ?? remoteName, isConnected);
        }
    }

    private void OnReceived(PeerReceivedEventArgs args)
    {
        if (!connections.Contains(args.Connection) || args.Connection.User is not { } user) { return; }
        string remoteName = user.Name;
        if (closedNames.ContainsKey(remoteName)) { return; }

        // An empty payload is a PeerConnectionMonitor heartbeat, not a real message to relay.
        if (args.Payload.IsEmpty) { return; }

        ReadOnlyMemory<byte> copy = args.Payload;
        if (IsChild(remoteName))
        {
            _ = Task.Run(() => Relay(() => HandleFromChild(remoteName, copy)));
        }
        else
        {
            _ = Task.Run(() => Relay(() => HandleFromServer(remoteName, copy)));
        }
    }

    private async Task Relay(Func<Task> relay)
    {
        try { await relay(); }
        catch (Exception ex) { logger.LogError(ex, "Failed to relay a message"); }
    }

    private async Task HandleFromChild(string childName, ReadOnlyMemory<byte> data)
    {
        object? message = TryDeserialize(data);
        if (message is null) { return; }

        HashSet<string> addressedUsers = GetAddressedUsers(message);
        int priority = engineController.GetPriority(message);
        string myName = currentUserProvider.UserName ?? string.Empty;

        if (!userMap.TryGetValue(myName, out _)) { return; }

        if (engineController.IsRetrieval(message))
        {
            await RouteRetrieval(childName, message, data, addressedUsers, priority, forward: true);
            return;
        }

        await storage.Store(message);
        await RouteFromChild(message, data);
    }

    private async Task RouteFromChild(object message, ReadOnlyMemory<byte> data)
    {
        HashSet<string> addressedUsers = GetAddressedUsers(message);
        int priority = engineController.GetPriority(message);
        string myName = currentUserProvider.UserName ?? string.Empty;

        if (!userMap.TryGetValue(myName, out ServerUserConfig? myConfig)) { return; }

        List<Task> sends = [.. addressedUsers
            .Where(user => myConfig.ChildClients.Contains(user, StringComparer.OrdinalIgnoreCase))
            .Select(user => TrySend(user, data, priority))];

        foreach ((string serverName, ServerUserConfig config) in userMap)
        {
            if (string.Equals(serverName, myName, StringComparison.OrdinalIgnoreCase)) { continue; }
            if (config.ChildClients.Any(child => addressedUsers.Contains(child)))
            {
                sends.Add(TrySend(serverName, data, priority));
            }
        }

        // Recipients are reached concurrently, so one that is slow or unreachable does not hold up the others.
        await Task.WhenAll(sends);
    }

    private async Task HandleFromServer(string serverName, ReadOnlyMemory<byte> data)
    {
        object? message = TryDeserialize(data);
        if (message is null) { return; }

        HashSet<string> addressedUsers = GetAddressedUsers(message);
        int priority = engineController.GetPriority(message);
        string myName = currentUserProvider.UserName ?? string.Empty;

        if (!userMap.TryGetValue(myName, out ServerUserConfig? myConfig)) { return; }

        if (engineController.IsRetrieval(message))
        {
            await RouteRetrieval(engineController.GetFromUser(message), message, data, addressedUsers, priority, forward: false);
            return;
        }

        await storage.Store(message);

        await Task.WhenAll(addressedUsers
            .Where(user => myConfig.ChildClients.Contains(user, StringComparer.OrdinalIgnoreCase))
            .Select(user => TrySend(user, data, priority)));
    }

    // A retrieval request is addressed to a server, not to any child client, so ordinary routing would drop it: this
    // server answers it if it is addressed (and stores), and, for a request that came from a child, hands it on to
    // every other addressed server, which answers it the same way.
    private async Task RouteRetrieval(string requester, object request, ReadOnlyMemory<byte> data, HashSet<string> addressedUsers, int priority, bool forward)
    {
        string myName = currentUserProvider.UserName ?? string.Empty;
        List<Task> work = [];

        if (addressedUsers.Contains(myName))
        {
            work.Add(AnswerRetrieval(requester, request));
        }

        if (forward)
        {
            work.AddRange(userMap.Keys
                .Where(server => !string.Equals(server, myName, StringComparison.OrdinalIgnoreCase) && addressedUsers.Contains(server))
                .Select(server => TrySend(server, data, priority)));
        }

        await Task.WhenAll(work);
    }

    private async Task AnswerRetrieval(string requester, object request)
    {
        foreach (object copy in await storage.Find(requester, request))
        {
            using IMemoryOwner<byte> buf = engineController.NetworkSerializer.Serialize(copy);
            await RouteFromChild(copy, buf.Memory);
        }
    }

    private HashSet<string> GetAddressedUsers(object message)
    {
        HashSet<string> addressed = new(engineController.GetAddresses(message).Where(a => a.Type != AddressType.External).Select(a => a.UserName), StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<SecurityLevel> securityLevels = engineController.SecurityLevels;
        int messageLevelRank = securityLevels.GetRank(engineController.GetSecurityLevel(message));
        if (messageLevelRank < 0) { return addressed; }

        List<string> blocked = [.. addressed.Where(user => securityLevels.GetRank(engineController.GetUserSecurityLevel(user)) < messageLevelRank)];
        if (blocked.Count > 0)
        {
            logger.LogWarning("Blocked relay to {Users}: security level not supported by destination", string.Join(", ", blocked));
            addressed.ExceptWith(blocked);
        }

        return addressed;
    }

    private object? TryDeserialize(ReadOnlyMemory<byte> data)
    {
        try
        {
            // NetworkSerializer determines the type from the data itself, so bytes from an incompatible
            // sender could describe a type other than this instance's own MessageType; treat that the
            // same as a failed deserialize rather than let a mismatched cast downstream throw.
            object? message = engineController.NetworkSerializer.Deserialize(data);
            return message?.GetType() == engineController.MessageType ? message : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task TrySend(string userName, ReadOnlyMemory<byte> data, int priority)
    {
        if (transport is null || closedNames.ContainsKey(userName)) { return; }
        if (connections.Get(userName) is not { } connection)
        {
            logger.LogWarning("Cannot deliver to {User}: no connection is identified as them", userName);
            return;
        }

        try { await transport.Request(connection, data, new PeerSendOptions { Priority = priority }, CancellationToken.None); }
        catch { }
    }

    /// <inheritdoc />
    public async Task<bool> SendPacket(string userName, object packet, CancellationToken cancellation = default)
    {
        if (transport is null || closedNames.ContainsKey(userName) || connections.Get(userName) is not { } connection) { return false; }

        try
        {
            using IMemoryOwner<byte> buf = engineController.PacketSerializer!.Serialize(packet);
            return await transport.Request(connection, buf.Memory, new PeerSendOptions { Priority = 0 }, cancellation);
        }
        catch
        {
            return false;
        }
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
                    LastDisconnectedAt = childLastDisconnectedAt.TryGetValue(childName, out DateTime disconnectedAt) ? disconnectedAt : null,
                    IsClosed = closedNames.ContainsKey(childName)
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
                LastDisconnectedAt = serverLastDisconnectedAt.TryGetValue(serverName, out DateTime disconnectedAt) ? disconnectedAt : null,
                IsClosed = closedNames.ContainsKey(serverName)
            });
        }

        return statuses;
    }

    /// <inheritdoc />
    public void SetClosed(PeerConnectionKind kind, string userName, bool closed)
    {
        if (transport is null || closedNames.ContainsKey(userName) == closed) { return; }
        if (!IsChild(userName) && !IsSiblingServer(userName)) { return; }

        if (closed)
        {
            closedNames[userName] = true;
            foreach (string key in GetPointKeys(userName))
            {
                if (points.TryGetValue(key, out ConnectionPoint? point)) { transport.SetClosed(point, true); }
                if (pointMonitors.TryGetValue(key, out PeerLinkControl? monitor)) { monitor.Close(); }
            }

            DropConnections(userName);
            UpdateStatusForName(userName, false);
        }
        else
        {
            closedNames.TryRemove(userName, out _);
            foreach (string key in GetPointKeys(userName))
            {
                if (points.TryGetValue(key, out ConnectionPoint? point)) { transport.SetClosed(point, false); }
                if (pointMonitors.TryGetValue(key, out PeerLinkControl? monitor)) { monitor.Open(); }
            }
        }

        StatusesChanged?.Invoke();
    }

    /// <inheritdoc />
    public void Refresh(PeerConnectionKind kind, string userName)
    {
        if (transport is null || closedNames.ContainsKey(userName)) { return; }
        if (!IsChild(userName) && !IsSiblingServer(userName)) { return; }

        foreach (string key in GetPointKeys(userName))
        {
            if (points.TryGetValue(key, out ConnectionPoint? point)) { transport.Reset(point); }
            if (pointMonitors.TryGetValue(key, out PeerLinkControl? monitor)) { monitor.Refresh(); }
        }

        DropConnections(userName);
    }

    private void DropConnections(string userName)
    {
        foreach (PeerConnection connection in connections.GetAll(userName)) { connection.Drop(); }
    }

    private async Task<bool> SendOnceAndCleanup(string messageId, object message, CancellationToken cancellation)
    {
        try
        {
            using IMemoryOwner<byte> buf = engineController.NetworkSerializer.Serialize(message);
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
        await MessageDelivered.InvokeAll(payload);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Registered as both IPeerService and IConnectionStatusService, so the container disposes it twice.
        if (Interlocked.Exchange(ref disposed, 1) != 0) { return; }
        if (transport is not null) { await transport.DisposeAsync(); }
    }
}
