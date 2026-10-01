namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// Implements <see cref="IPeerService"/> for <see cref="UserRole.Client"/>: sends every outbound message
/// over its one long-term connection to the server (the first of <see cref="IEngineController.OutgoingPoints"/>),
/// regardless of addressee - the server performs the actual user-to-connection routing. Connections are
/// bidirectional, so the server delivers messages back down the same connection and the client never listens.
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
        logger = loggerFactory.CreateLogger("ACTIVITY");
    }

    private readonly IPeerTransportFactory transportFactory;
    private readonly IEngineController engineController;
    private readonly ILogger logger;
    private readonly PointMaintenance points;
    private readonly Lock reconfigureLock = new();

    private readonly ConcurrentDictionary<string, Task<bool>> inFlightSends = new();

    private IPeerTransport? transport;
    private ConnectionPoint? serverPoint;
    private PeerConnection? serverConnection;
    private PeerLinkControl? serverLink;
    private volatile bool isClosed;
    private CancellationToken lifetime;
    private volatile string serverName = string.Empty;
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
        ConnectionPoint? point = engineController.OutgoingPoints.FirstOrDefault();
        if (point is null)
        {
            logger.LogError("Client role requires an outgoing connection point to its server; none was provided");
            return;
        }

        transport = transportFactory.Create();
        transport.Connected.Listen(OnConnected);
        transport.Disconnected.Listen(OnDisconnected);
        transport.Received.Listen(OnReceived);
        lock (reconfigureLock)
        {
            lifetime = cancellation;
            (_, IReadOnlyList<(ConnectionPoint Point, PeerLinkControl Control)> started) = points.Sync(transport, [point], lifetime, OnHeartbeatAcknowledged, startingPoint => serverPoint = startingPoint);
            serverPoint = started[0].Point;
            serverLink = started[0].Control;
        }

        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
    }

    /// <inheritdoc />
    public Task<bool> Send(string userName, object message, CancellationToken cancellation = default)
    {
        // Route()/MessageRoutingService calls Send once per resolved recipient, even for a single group
        // address expanding to several users; since every send here goes to the one shared server
        // regardless of userName, in-flight sends are coalesced by message ID to avoid transmitting the
        // same message multiple times.
        string messageId = engineController.GetFrameId(message);
        return inFlightSends.GetOrAdd(messageId, _ => SendOnceAndCleanup(messageId, message, cancellation));
    }

    private async Task<bool> SendOnceAndCleanup(string messageId, object message, CancellationToken cancellation)
    {
        try { return await SendOnce(messageId, message, cancellation); }
        finally { inFlightSends.TryRemove(messageId, out _); }
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

            ConnectionPoint? wanted = engineController.OutgoingPoints.FirstOrDefault();
            if (wanted is null) { logger.LogError("Client role requires an outgoing connection point to its server; none is defined any more"); }

            (IReadOnlyList<ConnectionPoint> removed, IReadOnlyList<(ConnectionPoint Point, PeerLinkControl Control)> started) = points.Sync(transport, wanted is null ? [] : [wanted], lifetime, OnHeartbeatAcknowledged, startingPoint => serverPoint = startingPoint);
            if (removed.Count == 0 && started.Count == 0) { return; }

            if (removed.Count > 0)
            {
                serverConnection?.Drop();
                UpdateConnectionStatus(false);
                serverPoint = started.Count > 0 ? started[0].Point : null;
                serverLink = null;
                isClosed = false;
            }

            if (started.Count > 0)
            {
                serverPoint = started[0].Point;
                serverLink = started[0].Control;
            }

            StatusesChanged?.Invoke();
        }
    }

    /// <inheritdoc />
    public void SetClosed(PeerConnectionKind kind, string userName, bool closed)
    {
        if (kind != PeerConnectionKind.Server || transport is null || serverPoint is null || serverLink is null || isClosed == closed) { return; }

        isClosed = closed;
        transport.SetClosed(serverPoint, closed);
        if (closed)
        {
            serverLink.Close();
            serverConnection?.Drop();
            UpdateConnectionStatus(false);
        }
        else
        {
            serverLink.Open();
        }

        StatusesChanged?.Invoke();
    }

    /// <inheritdoc />
    public void Refresh(PeerConnectionKind kind, string userName)
    {
        if (kind != PeerConnectionKind.Server || transport is null || serverPoint is null || serverLink is null || isClosed) { return; }

        transport.Reset(serverPoint);
        serverLink.Refresh();
    }

    private async Task<bool> SendOnce(string messageId, object message, CancellationToken cancellation)
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
            return await transport.Request(connection, buf.Memory, new PeerSendOptions { Priority = 0 }, cancellation);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task DeliverLocal(object payload)
    {
        logger.LogInformation("{MessageId} delivered locally from {FromUser}", engineController.GetFrameId(payload), engineController.GetFromUser(payload));
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
        if (connection.IsSerial)
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
        serverLink?.NotifyLost();
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

        if (connected) { logger.LogInformation("Connected to server"); }
        else { logger.LogWarning("Server unreachable"); }
        StatusesChanged?.Invoke();
        PeerConnectionNotifier.Raise(connected ? UserConnected : UserDisconnected, serverName, connected ? "connecting" : "disconnecting", logger);
    }

    private bool IsServerConnection(PeerConnection connection)
        => !connection.IsInbound && serverPoint is { } expected && expected.Equals(connection.Point);

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
