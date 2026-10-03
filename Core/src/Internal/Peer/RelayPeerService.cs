namespace BlueHeighliner.Comlink;

/// <summary>
/// Implements <see cref="IPeerService"/> for <see cref="UserRole.Relay"/>: a direct network path between its child clients and the one server it is linked to. It listens on
/// <see cref="IEngineController.PeerPort"/>, accepts the clients named in the current user's <see cref="UserInfo.Children"/> (or dials those whose link says so) and keeps a connection open to
/// its parent, the server (see <see cref="IEngineController.ParentPoints"/>). Everything a child sends is forwarded to the server, and everything the server sends is forwarded
/// to whichever of the children it addresses, as the very bytes that arrived: nothing is modified, stored, receipted or delivered locally, and the relay never composes traffic of
/// its own. Frames are only deserialized to read their addresses and priority, and a frame that addresses no one is not traffic and is not forwarded: the initial packet and frame exchange that introduces
/// the two ends of a connection is point-to-point and consumed by the transport before the relay sees anything, and anything of that kind that did slip through carries no addresses. Tracks connect/disconnect status for each child and the server, kept live by a
/// <see cref="PeerConnectionMonitor"/> heartbeat on the server connection. See <c>Docs/Components/Peer.md</c>.
/// </summary>
internal sealed class RelayPeerService : IPeerService, IConnectionStatusService, IReconfigurable, IAsyncDisposable
{
    /// <summary>Initializes a new <see cref="RelayPeerService"/>.</summary>
    public RelayPeerService(IPeerTransportFactory transportFactory, IEngineController engineController, ICurrentUserProvider currentUserProvider, ILoggerFactory loggerFactory)
    {
        this.transportFactory = transportFactory;
        this.engineController = engineController;
        this.currentUserProvider = currentUserProvider;
        points = new PointMaintenance(new PeerConnectionMonitor(engineController));
        logger = loggerFactory.CreateLogger("ACTIVITY");
    }

    private readonly IPeerTransportFactory transportFactory;
    private readonly IEngineController engineController;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly ILogger logger;
    private readonly PointMaintenance points;
    private readonly Lock reconfigureLock = new();
    private readonly Lock statusLock = new();
    private readonly UserConnections connections = new();
    private readonly ConcurrentDictionary<string, bool> childConnected = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> childLastConnectedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> childLastDisconnectedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> closedChildren = new(StringComparer.OrdinalIgnoreCase);

    private IPeerTransport? transport;
    private readonly ParentLinkSet parentLinks = new();
    private PeerConnection? serverConnection;
    private volatile bool isServerClosed;
    private CancellationToken lifetime;
    private int listenPort;
    private volatile string serverName = string.Empty;
    private bool isServerConnected;
    private DateTime? serverLastConnectedAt;
    private DateTime? serverLastDisconnectedAt;
    private int disposed;

#pragma warning disable CS0067 // A relay delivers nothing locally and tracks no delivery status, so these never fire.
    /// <inheritdoc />
    public event Func<object, Task>? FrameDelivered;
    /// <inheritdoc />
    public event Func<string, string, Task>? ReadReceiptReceived;
    /// <inheritdoc />
    public event Func<string, string, Task>? ReceiveReceiptReceived;
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
    public IReadOnlyList<string> GetConnectedUsers()
    {
        lock (statusLock) { return [.. childConnected.Keys, .. (isServerConnected ? new[] { serverName } : [])]; }
    }

    /// <inheritdoc />
    public bool IsUserConnected(string userName)
    {
        lock (statusLock) { return childConnected.ContainsKey(userName) || (isServerConnected && string.Equals(serverName, userName, StringComparison.OrdinalIgnoreCase)); }
    }

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        if (engineController.ParentUser is null)
        {
            logger.LogError("Relay role requires a parent (its server); none was provided");
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
            serverName = engineController.ParentUser;
            HashSet<string> parentKeys = [.. engineController.ParentPoints.Select(point => point.Key)];
            (_, IReadOnlyList<(ConnectionPoint Point, PeerLinkControl Control)> started) = points.Sync(transport, engineController.OutgoingPoints, lifetime, OnHeartbeatAcknowledged, point => { if (parentKeys.Contains(point.Key)) { parentLinks.Track(point); } });
            parentLinks.Attach(started.Where(entry => parentKeys.Contains(entry.Point.Key)));
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

            bool changed = DropFormerChildren();
            if (engineController.PeerPort != listenPort)
            {
                transport.StopListener();
                listenPort = engineController.PeerPort;
                transport.StartListener(listenPort);
            }

            if (engineController.ParentUser is null) { logger.LogError("Relay role requires a parent (its server); none is defined any more"); }

            HashSet<string> parentKeys = [.. engineController.ParentPoints.Select(point => point.Key)];
            (IReadOnlyList<ConnectionPoint> removed, IReadOnlyList<(ConnectionPoint Point, PeerLinkControl Control)> started) = points.Sync(transport, engineController.OutgoingPoints, lifetime, OnHeartbeatAcknowledged, point => { if (parentKeys.Contains(point.Key)) { parentLinks.Track(point); } });
            if (removed.Any(point => parentLinks.Contains(point)) || !string.Equals(serverName, engineController.ParentUser, StringComparison.OrdinalIgnoreCase))
            {
                serverConnection?.Drop();
                UpdateServerStatus(false);
                parentLinks.Remove(removed);
                isServerClosed = false;
                serverName = engineController.ParentUser ?? string.Empty;
            }

            parentLinks.Attach(started.Where(entry => parentKeys.Contains(entry.Point.Key)));

            if (changed || removed.Count > 0 || started.Count > 0) { StatusesChanged?.Invoke(); }
        }
    }

    // Children the topology no longer lists are disconnected and forgotten; everyone else keeps their connection and status.
    private bool DropFormerChildren()
    {
        List<string> former = [.. childConnected.Keys.Concat(childLastConnectedAt.Keys).Concat(childLastDisconnectedAt.Keys).Concat(closedChildren.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Where(name => FindChild(name) is null)];
        foreach (string name in former)
        {
            foreach (PeerConnection connection in connections.GetAll(name)) { connection.Drop(); }
            childConnected.TryRemove(name, out _);
            childLastConnectedAt.TryRemove(name, out _);
            childLastDisconnectedAt.TryRemove(name, out _);
            closedChildren.TryRemove(name, out _);
        }

        return former.Count > 0;
    }

    private IReadOnlyList<string> GetChildNames() => currentUserProvider.UserName is { } name ? [.. engineController.GetUserInfo(name).Children.Select(link => link.User)] : [];

    private string? FindChild(string name) => GetChildNames().FirstOrDefault(child => string.Equals(child, name, StringComparison.OrdinalIgnoreCase));

    private bool IsServerConnection(PeerConnection connection)
        => connection.IsInbound
            ? engineController.ParentUser is { } parent && string.Equals(connection.User?.Name, parent, StringComparison.OrdinalIgnoreCase)
            : connection.IsSerial
                ? engineController.ParentUser is { } serialParent && string.Equals(connection.User?.Name, serialParent, StringComparison.OrdinalIgnoreCase)
                : parentLinks.Contains(connection.Point);

    private void OnConnected(PeerConnectionEventArgs args)
    {
        PeerConnection connection = args.Connection;
        if (IsServerConnection(connection))
        {
            if (isServerClosed)
            {
                connection.Drop();
                return;
            }

            serverConnection = connection;
            if (connection.User is { } server) { serverName = server.Name; }

            // An IP connection only counts as up once a heartbeat is acknowledged (see OnHeartbeatAcknowledged); a serial link only comes up when the far end answers.
            if (connection.IsSerial || connection.IsInbound) { UpdateServerStatus(true); }
            return;
        }

        string name = connection.User?.Name ?? string.Empty;
        if (FindChild(name) is not { } child)
        {
            logger.LogWarning("Rejected connection from {Name}, which is neither its parent nor one of its children", name);
            connection.Drop();
            return;
        }

        if (closedChildren.ContainsKey(child))
        {
            connection.Drop();
            return;
        }

        connections.Add(connection);

        // A connection this relay dialed to a child only counts as up once a heartbeat is acknowledged (see OnHeartbeatAcknowledged), like the one to its server.
        if (connection.IsInbound || connection.IsSerial) { UpdateChildStatus(child, true); }
    }

    private void OnHeartbeatAcknowledged(PeerConnection connection)
    {
        if (ReferenceEquals(connection, serverConnection))
        {
            if (!isServerClosed) { UpdateServerStatus(true); }
        }
        else if (connections.Contains(connection) && connection.User is { } user && FindChild(user.Name) is { } child && !closedChildren.ContainsKey(child))
        {
            UpdateChildStatus(child, true);
        }
    }

    private void OnDisconnected(PeerConnectionEventArgs args)
    {
        if (ReferenceEquals(args.Connection, serverConnection))
        {
            serverConnection = null;
            UpdateServerStatus(false);
            parentLinks.NotifyLost();
            return;
        }

        if (connections.Remove(args.Connection) is not { } name || connections.Has(name)) { return; }

        UpdateChildStatus(FindChild(name) ?? name, false);
    }

    private void UpdateServerStatus(bool connected)
    {
        lock (statusLock)
        {
            if (isServerConnected == connected) { return; }

            isServerConnected = connected;
            if (connected) { serverLastConnectedAt = DateTime.UtcNow; }
            else { serverLastDisconnectedAt = DateTime.UtcNow; }
        }

        if (connected) { logger.LogInformation("Connected to server"); }
        else { logger.LogWarning("Server unreachable"); }
        StatusesChanged?.Invoke();
        PeerConnectionNotifier.Raise(connected ? UserConnected : UserDisconnected, serverName, connected ? "connecting" : "disconnecting", logger);
    }

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

    private void OnReceived(PeerReceivedEventArgs args)
    {
        ReadOnlyMemory<byte> data = args.Payload;
        object? packet = args.Packet;
        if (ReferenceEquals(args.Connection, serverConnection))
        {
            if (!isServerClosed) { _ = Task.Run(() => Relay(() => ForwardFromServer(data, packet))); }
        }
        else if (connections.Contains(args.Connection) && args.Connection.User is { } user && FindChild(user.Name) is { } child && !closedChildren.ContainsKey(child))
        {
            _ = Task.Run(() => Relay(() => ForwardFromChild(data, packet)));
        }
    }

    private async Task Relay(Func<Task> relay)
    {
        try { await relay(); }
        catch (Exception ex) { logger.LogError(ex, "Failed to forward a message"); }
    }

    private async Task ForwardFromChild(ReadOnlyMemory<byte> data, object? packet)
    {
        object? frame = TryDeserialize(data, packet);
        PeerConnection? connection = serverConnection;
        if (frame is null || engineController.IsHeartbeat(frame) || engineController.GetAddresses(frame).Count == 0) { return; }

        if (transport is null || connection is null || isServerClosed)
        {
            logger.LogWarning("Cannot forward {MessageId} from {FromUser}: the server is unreachable", engineController.GetFrameId(frame), engineController.GetFromUser(frame));
            return;
        }

        try { await transport.Request(connection, data, new PeerSendOptions { Priority = engineController.GetPriority(frame), Frame = frame }, CancellationToken.None); }
        catch { }
    }

    private async Task ForwardFromServer(ReadOnlyMemory<byte> data, object? packet)
    {
        object? frame = TryDeserialize(data, packet);
        if (frame is null || engineController.IsHeartbeat(frame) || transport is null) { return; }

        HashSet<string> addressed = new(engineController.GetAddresses(frame).Where(address => address.Type != AddressType.External).Select(address => address.UserName), StringComparer.OrdinalIgnoreCase);
        int priority = engineController.GetPriority(frame);
        IEnumerable<Task> sends = GetChildNames()
            .Where(child => addressed.Contains(child) && !closedChildren.ContainsKey(child))
            .Select(child => TrySend(child, data, priority, frame));

        // Children are reached concurrently, so one that is slow or unreachable does not hold up the others.
        await Task.WhenAll(sends);
    }

    private async Task TrySend(string child, ReadOnlyMemory<byte> data, int priority, object frame)
    {
        if (connections.Get(child) is not { } connection)
        {
            logger.LogWarning("Cannot deliver to {User}: no connection is identified as them", child);
            return;
        }

        try { await transport!.Request(connection, data, new PeerSendOptions { Priority = priority, Frame = frame }, CancellationToken.None); }
        catch { }
    }

    private object? TryDeserialize(ReadOnlyMemory<byte> data, object? packet)
    {
        try
        {
            object frame = engineController.FrameSerializer.Deserialize(data, packet);
            return frame.GetType() == engineController.FrameType ? frame : null;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public Task<bool> Send(string userName, object message, CancellationToken cancellation = default) => Task.FromResult(false);

    /// <inheritdoc />
    public Task<bool> SendPacket(string userName, object packet, CancellationToken cancellation = default) => Task.FromResult(false);

    /// <inheritdoc />
    public Task DeliverLocal(object payload) => Task.CompletedTask;

    /// <inheritdoc />
    public IReadOnlyList<PeerConnectionStatus> GetStatuses()
    {
        List<PeerConnectionStatus> statuses = [];
        lock (statusLock)
        {
            foreach (string child in GetChildNames())
            {
                statuses.Add(new PeerConnectionStatus
                {
                    UserName = child,
                    Kind = PeerConnectionKind.Client,
                    IsConnected = childConnected.ContainsKey(child),
                    LastConnectedAt = childLastConnectedAt.TryGetValue(child, out DateTime connectedAt) ? connectedAt : null,
                    LastDisconnectedAt = childLastDisconnectedAt.TryGetValue(child, out DateTime disconnectedAt) ? disconnectedAt : null,
                    IsClosed = closedChildren.ContainsKey(child)
                });
            }

            statuses.Add(new PeerConnectionStatus
            {
                UserName = serverName,
                Kind = PeerConnectionKind.Server,
                IsConnected = isServerConnected,
                LastConnectedAt = serverLastConnectedAt,
                LastDisconnectedAt = serverLastDisconnectedAt,
                IsClosed = isServerClosed
            });
        }

        return statuses;
    }

    /// <inheritdoc />
    public void SetClosed(PeerConnectionKind kind, string userName, bool closed)
    {
        if (transport is null) { return; }

        if (kind == PeerConnectionKind.Server)
        {
            if (isServerClosed == closed) { return; }

            isServerClosed = closed;
            parentLinks.SetClosed(transport, closed);
            if (closed)
            {
                serverConnection?.Drop();
                UpdateServerStatus(false);
            }

            StatusesChanged?.Invoke();
            return;
        }

        if (FindChild(userName) is not { } child || closedChildren.ContainsKey(child) == closed) { return; }

        if (closed)
        {
            closedChildren[child] = true;
            foreach (PeerConnection connection in connections.GetAll(child)) { connection.Drop(); }
            UpdateChildStatus(child, false);
        }
        else
        {
            closedChildren.TryRemove(child, out _);
        }

        StatusesChanged?.Invoke();
    }

    /// <inheritdoc />
    public void Refresh(PeerConnectionKind kind, string userName)
    {
        if (transport is null) { return; }

        if (kind == PeerConnectionKind.Server)
        {
            if (isServerClosed) { return; }

            if (!parentLinks.HasPoints) { serverConnection?.Drop(); }
            else { parentLinks.Refresh(transport); }
            return;
        }

        if (FindChild(userName) is not { } child || closedChildren.ContainsKey(child)) { return; }

        foreach (PeerConnection connection in connections.GetAll(child)) { connection.Drop(); }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Registered as both IPeerService and IConnectionStatusService, so the container disposes it twice.
        if (Interlocked.Exchange(ref disposed, 1) != 0) { return; }
        if (transport is not null) { await transport.DisposeAsync(); }
    }
}
