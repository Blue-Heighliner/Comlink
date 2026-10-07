namespace BlueHeighliner.Comlink;

/// <summary>
/// Implements <see cref="IPeerService"/> for <see cref="UserRole.Client"/>: sends every outbound message
/// over its one long-term connection to the server (its parent, see <see cref="IEngineController.ParentPoints"/>),
/// regardless of addressee - the server performs the actual user-to-connection routing. Connections are
/// bidirectional, so the server delivers messages back down the same connection. The client only listens when its parent's link is forced to listen mode, in which case the server connects to it.
/// A background <see cref="PeerConnectionMonitor"/> proactively opens and maintains that connection with a
/// recurring heartbeat, independent of whether any real message is being sent, so <see cref="GetStatuses"/> reflects
/// the connection's live state continuously rather than only the moment a message last happened to flow. Who the
/// server is comes from identifying the connection, the same as for any other. See <c>Docs/Components/Peer.md</c>.
/// </summary>
internal sealed class ClientPeerService : IPeerService, IConnectionStatusService, IReconfigurable, IAsyncDisposable
{
    /// <summary>Initializes a new <see cref="ClientPeerService"/>.</summary>
    public ClientPeerService(
        IPeerTransportFactory transportFactory,
        IEngineController engineController,
        ILoggerFactory loggerFactory)
    {
        this.transportFactory = transportFactory;
        this.engineController = engineController;
        points = new PointMaintenance(new PeerConnectionMonitor(engineController));
        logger = loggerFactory.CreateLogger(LogCategories.App);
    }

    private readonly IPeerTransportFactory transportFactory;
    private readonly IEngineController engineController;
    private readonly ILogger logger;
    private readonly PointMaintenance points;
    private readonly Lock reconfigureLock = new();

    private readonly ConcurrentDictionary<object, Task<bool>> inFlightSends = new(ReferenceEqualityComparer.Instance);

    private IPeerTransport? transport;
    private readonly ParentLinkSet parentLinks = new();
    private PeerConnection? serverConnection;
    private volatile bool isClosed;
    private CancellationToken lifetime;
    private volatile string serverName = string.Empty;
    private int listenPort;
    private readonly Lock statusLock = new();
    private bool isConnected;
    private DateTime? lastConnectedAt;
    private DateTime? lastDisconnectedAt;
    private int disposed;

    /// <inheritdoc />
    public event Func<object, Task>? FrameDelivered;

    /// <inheritdoc />
    public event Func<string, string, Task>? ReadReceiptReceived;

    /// <inheritdoc />
    public event Func<string, string, Task>? ReceiveReceiptReceived;

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
    public IReadOnlyList<string> GetConnectedUsers()
    {
        lock (statusLock) { return isConnected ? [serverName] : []; }
    }

    /// <inheritdoc />
    public bool IsUserConnected(string userName)
    {
        lock (statusLock) { return isConnected && string.Equals(serverName, userName, StringComparison.OrdinalIgnoreCase); }
    }

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        IReadOnlyList<ConnectionPoint> parentPoints = engineController.ParentPoints;
        if (parentPoints.Count == 0 && engineController.ParentUser is null)
        {
            logger.Record(LogEvents.InvalidConfigurationFile, "Invalid configuration file: {Problem}", "client role requires a parent (its server); none was provided");
            logger.Record(LogEvents.NetworkingNotWorking, "Networking is not working: {Reason}", "the network configuration is not valid");
            return;
        }

        transport = transportFactory.Create();
        transport.Connected.Listen(OnConnected);
        transport.Disconnected.Listen(OnDisconnected);
        transport.Received.Listen(OnReceived);
        lock (reconfigureLock)
        {
            lifetime = cancellation;
            if (parentPoints.Count == 0)
            {
                serverName = engineController.ParentUser!;
                listenPort = engineController.PeerPort;
                transport.StartListener(listenPort);
            }
            else
            {
                (_, IReadOnlyList<(ConnectionPoint Point, PeerLinkControl Control)> started) = points.Sync(transport, parentPoints, lifetime, OnHeartbeatAcknowledged, parentLinks.Track);
                parentLinks.Attach(started);
            }
        }

        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
    }

    /// <inheritdoc />
    public Task<bool> Send(string userName, object message, CancellationToken cancellation = default)
    {
        // Route()/MessageRoutingService calls Send once per resolved recipient, even for a single group
        // address expanding to several users; since every send here goes to the one shared server
        // regardless of userName, in-flight sends are coalesced by frame instance to avoid transmitting the
        // same message multiple times.
        return inFlightSends.GetOrAdd(message, _ => SendOnceAndCleanup(message, cancellation));
    }

    private async Task<bool> SendOnceAndCleanup(object message, CancellationToken cancellation)
    {
        try { return await SendOnce(message, cancellation); }
        finally { inFlightSends.TryRemove(message, out _); }
    }

    /// <inheritdoc />
    public IReadOnlyList<PeerConnectionStatus> GetStatuses()
    {
        lock (statusLock)
        {
            return [new PeerConnectionStatus
            {
                UserName = serverName,
                Kind = PeerConnectionKind.Server,
                IsConnected = isConnected,
                LastConnectedAt = lastConnectedAt,
                LastDisconnectedAt = lastDisconnectedAt,
                IsClosed = isClosed
            }];
        }
    }

    /// <inheritdoc />
    public void Reconfigure()
    {
        lock (reconfigureLock)
        {
            if (transport is null || lifetime == default) { return; }

            IReadOnlyList<ConnectionPoint> wanted = engineController.ParentPoints;
            if (wanted.Count == 0 && engineController.ParentUser is null)
            {
                logger.Record(LogEvents.InvalidConfigurationFile, "Invalid configuration file: {Problem}", "client role requires a parent (its server); none is defined any more");
                logger.Record(LogEvents.NetworkingNotWorking, "Networking is not working: {Reason}", "the network configuration is not valid");
            }
            if (wanted.Count == 0 && engineController.ParentUser is not null && engineController.PeerPort != listenPort)
            {
                transport.StopListener();
                listenPort = engineController.PeerPort;
                transport.StartListener(listenPort);
            }

            (IReadOnlyList<ConnectionPoint> removed, IReadOnlyList<(ConnectionPoint Point, PeerLinkControl Control)> started) = points.Sync(transport, wanted, lifetime, OnHeartbeatAcknowledged, parentLinks.Track);
            if (removed.Count == 0 && started.Count == 0) { return; }

            if (removed.Count > 0)
            {
                serverConnection?.Drop();
                UpdateConnectionStatus(false);
                parentLinks.Remove(removed);
                isClosed = false;
            }

            parentLinks.Attach(started);

            StatusesChanged?.Invoke();
        }
    }

    /// <inheritdoc />
    public void SetClosed(PeerConnectionKind kind, string userName, bool closed)
    {
        if (kind != PeerConnectionKind.Server || transport is null || isClosed == closed) { return; }

        isClosed = closed;
        parentLinks.SetClosed(transport, closed);
        if (closed)
        {
            serverConnection?.Drop();
            UpdateConnectionStatus(false);
        }

        StatusesChanged?.Invoke();
    }

    /// <inheritdoc />
    public void Refresh(PeerConnectionKind kind, string userName)
    {
        if (kind != PeerConnectionKind.Server || transport is null || isClosed) { return; }

        if (!parentLinks.HasPoints)
        {
            serverConnection?.Drop();
            return;
        }

        parentLinks.Refresh(transport);
    }

    private async Task<bool> SendOnce(object message, CancellationToken cancellation)
    {
        PeerConnection? connection = serverConnection;
        if (transport is null || connection is null || isClosed) { return false; }

        try
        {
            using IMemoryOwner<byte> buf = engineController.FrameSerializer.Serialize(message);
            return await transport.Request(connection, buf.Memory, new PeerSendOptions { Priority = engineController.GetPriority(message), Frame = message }, cancellation);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SendPacket(string userName, object packet, CancellationToken cancellation = default)
    {
        // Ignores userName, the same as Send: every send here goes over the one connection to the server, which
        // performs the actual user-to-connection routing.
        PeerConnection? connection = serverConnection;
        if (transport is null || connection is null || isClosed) { return false; }

        try
        {
            using IMemoryOwner<byte> buf = engineController.PacketSerializer!.Serialize(packet, null);
            return await transport.Request(connection, buf.Memory, new PeerSendOptions { Priority = engineController.LowestPriority }, cancellation);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task DeliverLocal(object payload)
    {
        logger.Record(LogEvents.MessageDeliveredLocally, "{MessageId} delivered locally from {FromUser}", engineController.GetIdentifier(payload), engineController.GetFromUser(payload));
        await FrameDelivered.InvokeAll(payload);
    }

    private void OnConnected(PeerConnectionEventArgs args)
    {
        PeerConnection connection = args.Connection;
        if (!IsServerConnection(connection)) { return; }

        if (isClosed)
        {
            connection.Drop();
            return;
        }

        serverConnection = connection;
        if (connection.User is { } user) { serverName = user.Name; }

        // An IP connection only counts as up once a heartbeat is acknowledged (see OnHeartbeatAcknowledged); a serial
        // link is cabled to exactly one node and only ever comes up when that node answers, so it is up immediately.
        if (connection.IsSerial || connection.IsInbound)
        {
            UpdateConnectionStatus(true);
        }
    }

    private void OnHeartbeatAcknowledged(PeerConnection connection)
    {
        if (!isClosed && ReferenceEquals(connection, serverConnection)) { UpdateConnectionStatus(true); }
    }

    private void OnDisconnected(PeerConnectionEventArgs args)
    {
        if (!ReferenceEquals(args.Connection, serverConnection)) { return; }

        serverConnection = null;
        UpdateConnectionStatus(false);

        // An unexpected drop (as opposed to this node's own Close/Refresh action, which already wakes the
        // monitor itself) would otherwise sit unnoticed until the monitor's current heartbeat interval elapses -
        // up to steadyInterval - since nothing else wakes a sleeping monitor. Waking it here lets it retry (and
        // report the reconnect) right away instead.
        parentLinks.NotifyLost();
    }

    private void UpdateConnectionStatus(bool connected)
    {
        lock (statusLock)
        {
            if (isConnected == connected) { return; }

            isConnected = connected;
            if (connected) { lastConnectedAt = DateTime.UtcNow; }
            else { lastDisconnectedAt = DateTime.UtcNow; }
        }

        logger.Record(LogEvents.ConnectionChanged, "{Change} {UserName}", connected ? "Connected to" : "Disconnected from", serverName);
        StatusesChanged?.Invoke();
        PeerConnectionNotifier.Raise(connected ? UserConnected : UserDisconnected, serverName, connected ? "connecting" : "disconnecting", logger);
    }

    private bool IsServerConnection(PeerConnection connection)
        => connection.IsInbound
            ? engineController.ParentUser is { } parent && string.Equals(connection.User?.Name, parent, StringComparison.OrdinalIgnoreCase)
            : connection.IsSerial
                ? engineController.ParentUser is { } serialParent && string.Equals(connection.User?.Name, serialParent, StringComparison.OrdinalIgnoreCase)
                : parentLinks.Contains(connection.Point);

    private void OnReceived(PeerReceivedEventArgs args)
    {
        if (!ReferenceEquals(args.Connection, serverConnection)) { return; }

        _ = Task.Run(() => HandleMessage(args.Payload, args.Packet));
    }

    internal Task<bool> HandleMessage(ReadOnlyMemory<byte> data, object? packet = null)
        => PeerFrameDispatcher.Dispatch(data, engineController, logger, FrameDelivered, ReadReceiptReceived, ReceiveReceiptReceived, packet);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Registered as both IPeerService and IConnectionStatusService, so the container disposes it twice.
        if (Interlocked.Exchange(ref disposed, 1) != 0) { return; }
        if (transport is not null) { await transport.DisposeAsync(); }
    }
}
