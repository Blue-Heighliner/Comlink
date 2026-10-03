namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// Implements <see cref="IPeerService"/> for <see cref="UserRole.Server"/>: listens on <see cref="IEngineController.PeerPort"/>
/// for connections from its child clients and other servers, keeps a connection open to each of its
/// <see cref="IEngineController.OutgoingPoints"/>, and relays raw message bytes between them. Nothing is configured
/// about where a child or another server is: a connection is matched to one by the identity it is given when it forms
/// (see <see cref="HandshakePeerTransport"/>), a connection whose identity is neither a child nor a server in the
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
internal sealed class ServerRoutingService : IPeerService, IConnectionStatusService, IReconfigurable, IAsyncDisposable
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
        maintenance = new PointMaintenance(new PeerConnectionMonitor(engineController));
        this.currentUserProvider = currentUserProvider;
        this.storage = storage;
        logger = loggerFactory.CreateLogger("ACTIVITY");
    }

    private readonly IPeerTransportFactory transportFactory;
    private readonly IEngineController engineController;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly IMessageStorageService storage;
    private readonly ILogger logger;
    private readonly PointMaintenance maintenance;
    private readonly Lock reconfigureLock = new();

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
    private CancellationToken lifetime;
    private int listenPort;
    private int disposed;

    /// <inheritdoc />
    public event Func<object, Task>? FrameDelivered;
#pragma warning disable CS0067 // A server relays raw message bytes without deserializing for receipt-vs-normal classification, so this never fires.
    /// <inheritdoc />
    public event Func<string, string, Task>? ReadReceiptReceived;
    /// <inheritdoc />
    public event Func<string, string, Task>? ReceiveReceiptReceived;
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
        lock (reconfigureLock)
        {
            lifetime = cancellation;
            listenPort = engineController.PeerPort;
            transport.StartListener(listenPort);
            SyncPoints();
        }

        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
    }

    /// <inheritdoc />
    public void Reconfigure()
    {
        lock (reconfigureLock)
        {
            if (transport is null || lifetime == default) { return; }

            bool changed = ApplyTopology();
            if (engineController.PeerPort != listenPort)
            {
                transport.StopListener();
                listenPort = engineController.PeerPort;
                transport.StartListener(listenPort);
            }

            changed |= SyncPoints();
            if (changed) { StatusesChanged?.Invoke(); }
        }
    }

    private bool SyncPoints()
    {
        (IReadOnlyList<ConnectionPoint> removed, IReadOnlyList<(ConnectionPoint Point, PeerLinkControl Control)> started) = maintenance.Sync(transport!, engineController.OutgoingPoints, lifetime, OnHeartbeatAcknowledged);
        foreach (ConnectionPoint point in removed)
        {
            points.TryRemove(point.Key, out _);
            pointMonitors.TryRemove(point.Key, out _);
            pointUsers.TryRemove(point.Key, out _);
        }

        foreach ((ConnectionPoint point, PeerLinkControl control) in started)
        {
            points[point.Key] = point;
            pointMonitors[point.Key] = control;
        }

        return removed.Count > 0 || started.Count > 0;
    }

    // Users the topology no longer lists as a child or another server are disconnected and forgotten; everyone else keeps their connection and status.
    private bool ApplyTopology()
    {
        IReadOnlyDictionary<string, ServerUserConfig> latest = engineController.Servers;
        bool changed = Describe(latest) != Describe(userMap);
        if (!changed) { return false; }

        userMap = latest;
        string myName = currentUserProvider.UserName ?? string.Empty;
        HashSet<string> valid = new(GetChildNames().Concat(latest.Keys.Where(server => !string.Equals(server, myName, StringComparison.OrdinalIgnoreCase))), StringComparer.OrdinalIgnoreCase);
        foreach (string name in new[] { childConnected, serverConnected }.SelectMany(map => map.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Where(name => !valid.Contains(name)).ToList())
        {
            PeerConnection? dropped = null;
            for (int attempt = 0; attempt < 2 && connections.Get(name) is { } connection && !ReferenceEquals(connection, dropped); attempt++)
            {
                connection.Drop();
                dropped = connection;
            }
        }

        foreach (ConcurrentDictionary<string, DateTime> map in new[] { childLastConnectedAt, childLastDisconnectedAt, serverLastConnectedAt, serverLastDisconnectedAt })
        {
            foreach (string name in map.Keys.Where(name => !valid.Contains(name)).ToList()) { map.TryRemove(name, out _); }
        }

        foreach (string name in closedNames.Keys.Where(name => !valid.Contains(name)).ToList()) { closedNames.TryRemove(name, out _); }
        return true;
    }

    private static string Describe(IReadOnlyDictionary<string, ServerUserConfig> map)
        => string.Join(';', map.OrderBy(server => server.Key, StringComparer.OrdinalIgnoreCase).Select(server => $"{server.Key}:{string.Join(',', server.Value.Children.Order(StringComparer.OrdinalIgnoreCase))}|{string.Join(',', server.Value.Relays.OrderBy(relay => relay.Key, StringComparer.OrdinalIgnoreCase).Select(relay => $"{relay.Key}={string.Join('+', relay.Value.Order(StringComparer.OrdinalIgnoreCase))}"))}"));

    private IReadOnlyList<string> GetChildNames()
        => userMap.TryGetValue(currentUserProvider.UserName ?? string.Empty, out ServerUserConfig? myConfig) ? myConfig.Children : [];

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

        ReadOnlyMemory<byte> copy = args.Payload;
        object? packet = args.Packet;
        if (IsChild(remoteName))
        {
            _ = Task.Run(() => Relay(() => HandleFromChild(remoteName, copy, packet)));
        }
        else
        {
            _ = Task.Run(() => Relay(() => HandleFromServer(remoteName, copy, packet)));
        }
    }

    private async Task Relay(Func<Task> relay)
    {
        try { await relay(); }
        catch (Exception ex) { logger.LogError(ex, "Failed to relay a message"); }
    }

    private async Task HandleFromChild(string childName, ReadOnlyMemory<byte> data, object? packet = null)
    {
        object? message = TryDeserialize(data, packet);
        if (message is null || engineController.IsHeartbeat(message)) { return; }

        HashSet<string> addressedUsers = GetAddressedUsers(message);
        int priority = engineController.GetPriority(message);
        string myName = currentUserProvider.UserName ?? string.Empty;

        if (!userMap.TryGetValue(myName, out ServerUserConfig? myConfig)) { return; }

        if (engineController.IsRetrieval(message))
        {
            await RouteRetrieval(myConfig.Relays.ContainsKey(childName) ? engineController.GetFromUser(message) : childName, message, data, addressedUsers, priority, forward: true);
            return;
        }

        await storage.Store(message);
        await RouteFromChild(message, data);
    }

    // The local users a message goes to: each addressed child client directly, and, once, each relay that has an addressed client behind it, which forwards the bytes on untouched.
    private IEnumerable<string> GetLocalTargets(ServerUserConfig config, HashSet<string> addressedUsers)
    {
        IEnumerable<string> direct = addressedUsers.Where(user => config.Children.Contains(user, StringComparer.OrdinalIgnoreCase));
        IEnumerable<string> viaRelays = config.Relays.Where(relay => relay.Value.Any(addressedUsers.Contains)).Select(relay => relay.Key);
        return direct.Concat(viaRelays).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private async Task RouteFromChild(object message, ReadOnlyMemory<byte> data)
    {
        HashSet<string> addressedUsers = GetAddressedUsers(message);
        int priority = engineController.GetPriority(message);
        string myName = currentUserProvider.UserName ?? string.Empty;

        if (!userMap.TryGetValue(myName, out ServerUserConfig? myConfig)) { return; }

        List<Task> sends = [.. GetLocalTargets(myConfig, addressedUsers).Select(user => TrySend(user, data, priority, message))];

        foreach ((string serverName, ServerUserConfig config) in userMap)
        {
            if (string.Equals(serverName, myName, StringComparison.OrdinalIgnoreCase)) { continue; }
            if (config.Children.Any(child => addressedUsers.Contains(child)) || config.Relays.Values.Any(clients => clients.Any(addressedUsers.Contains)))
            {
                sends.Add(TrySend(serverName, data, priority, message));
            }
        }

        // Recipients are reached concurrently, so one that is slow or unreachable does not hold up the others.
        await Task.WhenAll(sends);
    }

    private async Task HandleFromServer(string serverName, ReadOnlyMemory<byte> data, object? packet = null)
    {
        object? message = TryDeserialize(data, packet);
        if (message is null || engineController.IsHeartbeat(message)) { return; }

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

        await Task.WhenAll(GetLocalTargets(myConfig, addressedUsers).Select(user => TrySend(user, data, priority, message)));
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
                .Select(server => TrySend(server, data, priority, request)));
        }

        await Task.WhenAll(work);
    }

    private async Task AnswerRetrieval(string requester, object request)
    {
        foreach (object copy in await storage.Find(requester, request))
        {
            using IMemoryOwner<byte> buf = engineController.FrameSerializer.Serialize(copy);
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

    private object? TryDeserialize(ReadOnlyMemory<byte> data, object? packet)
    {
        try
        {
            // FrameSerializer determines the type from the data itself, so bytes from an incompatible
            // sender could describe a type other than this instance's own FrameType; treat that the
            // same as a failed deserialize rather than let a mismatched cast downstream throw.
            object message = engineController.FrameSerializer.Deserialize(data, packet);
            return message.GetType() == engineController.FrameType ? message : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task TrySend(string userName, ReadOnlyMemory<byte> data, int priority, object frame)
    {
        if (transport is null || closedNames.ContainsKey(userName)) { return; }
        if (connections.Get(userName) is not { } connection)
        {
            logger.LogWarning("Cannot deliver to {User}: no connection is identified as them", userName);
            return;
        }

        try { await transport.Request(connection, data, new PeerSendOptions { Priority = priority, Frame = frame }, CancellationToken.None); }
        catch { }
    }

    /// <inheritdoc />
    public async Task<bool> SendPacket(string userName, object packet, CancellationToken cancellation = default)
    {
        if (transport is null || closedNames.ContainsKey(userName) || connections.Get(userName) is not { } connection) { return false; }

        try
        {
            using IMemoryOwner<byte> buf = engineController.PacketSerializer!.Serialize(packet, null);
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
        string messageId = engineController.GetFrameId(message);
        return inFlightSends.GetOrAdd(messageId, _ => SendOnceAndCleanup(messageId, message, cancellation));
    }

    /// <inheritdoc />
    public IReadOnlyList<PeerConnectionStatus> GetStatuses()
    {
        string myName = currentUserProvider.UserName ?? string.Empty;
        List<PeerConnectionStatus> statuses = [];

        if (userMap.TryGetValue(myName, out ServerUserConfig? myConfig))
        {
            foreach (string childName in myConfig.Children)
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
            using IMemoryOwner<byte> buf = engineController.FrameSerializer.Serialize(message);
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
        logger.LogInformation("{MessageId} delivered locally from {FromUser}", engineController.GetFrameId(payload), engineController.GetFromUser(payload));
        await FrameDelivered.InvokeAll(payload);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Registered as both IPeerService and IConnectionStatusService, so the container disposes it twice.
        if (Interlocked.Exchange(ref disposed, 1) != 0) { return; }
        if (transport is not null) { await transport.DisposeAsync(); }
    }
}
