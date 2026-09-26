namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// Implements <see cref="IPeerService"/> for <see cref="NodeRole.Client"/>: sends every outbound message
/// to the configured server (<see cref="IEngineController"/>), regardless of addressee — the server
/// performs the actual user-to-connection routing. Over IP it also runs its own listener on <see
/// cref="IEngineController.PeerPort"/> so the server can deliver messages back to this client - MSMT's
/// client-request/server-response model means the server can never push over a connection this client
/// initiated, so a genuinely separate connection, dialed by the server back to this client, carries that
/// direction instead; a serial link is a single bidirectional cable and needs no such second connection.
/// A background <see cref="PeerConnectionMonitor"/> proactively opens and maintains a
/// connection to the server with a recurring heartbeat, independent of whether any real message is being
/// sent, so <see cref="GetStatuses"/> reflects the connection's live state continuously rather than only the
/// moment a message last happened to flow. See <c>Docs/Components/Peer.md</c>.
/// </summary>
internal sealed class ClientPeerService : IPeerService, IConnectionStatusService, IAsyncDisposable
{
    /// <summary>Initializes a new <see cref="ClientPeerService"/>.</summary>
    public ClientPeerService(
        IPeerTransportFactory transportFactory,
        IEngineController engineController,
        ILoggerFactory loggerFactory)
    {
        this.transportFactory = transportFactory;
        this.engineController = engineController;
        logger = loggerFactory.CreateLogger("ACTIVITY");
    }

    private readonly IPeerTransportFactory transportFactory;
    private readonly IEngineController engineController;
    private readonly ILogger logger;
    private readonly PeerConnectionMonitor connectionMonitor = new();

    private readonly ConcurrentDictionary<string, Task<bool>> inFlightSends = new();

    private readonly ConcurrentDictionary<PeerConnection, byte> inboundConnections = new();

    private IPeerTransport? transport;
    private UserEndpoint? serverEndpoint;
    private PeerLinkControl? serverLink;
    private volatile bool isClosed;
    private volatile string serverName = string.Empty;
    private volatile bool isConnected;
    private DateTime? lastConnectedAt;
    private DateTime? lastDisconnectedAt;

    /// <inheritdoc />
    public event Func<object, Task>? MessageDelivered;

    /// <inheritdoc />
    public event Func<string, string, Task>? ConfirmationReceived;

#pragma warning disable CS0067 // No per-message delivery status is tracked across the client/server hierarchy.
    /// <inheritdoc />
    public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;

#pragma warning restore CS0067
    /// <inheritdoc />
    public event Action? StatusesChanged;

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        UserEndpoint? endpoint = engineController.ServerEndpoint;
        if (endpoint is null)
        {
            logger.LogError("Client role requires a configured server endpoint; none was provided");
            return;
        }

        serverEndpoint = endpoint;
        if (endpoint.IsSerial) { serverName = endpoint.SerialPort!; }
        transport = transportFactory.Create();
        transport.Connected.Listen(OnConnected);
        transport.Disconnected.Listen(OnDisconnected);
        transport.Received.Listen(OnReceived);
        if (!endpoint.IsSerial)
        {
            transport.StartListener(engineController.PeerPort);
            logger.LogInformation("Client peer listening for server-originated deliveries");
        }
        serverLink = connectionMonitor.Maintain(transport, endpoint, cancellation, OnHeartbeatAcknowledged);

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
        string messageId = engineController.GetMessageId(message);
        return inFlightSends.GetOrAdd(messageId, _ => SendOnceAndCleanup(messageId, message, cancellation));
    }

    private async Task<bool> SendOnceAndCleanup(string messageId, object message, CancellationToken cancellation)
    {
        try { return await SendOnce(messageId, message, cancellation); }
        finally { inFlightSends.TryRemove(messageId, out _); }
    }

    /// <inheritdoc />
    public IReadOnlyList<PeerConnectionStatus> GetStatuses()
        => [new PeerConnectionStatus
        {
            UserName = serverName,
            Kind = PeerConnectionKind.Server,
            IsConnected = isConnected,
            LastConnectedAt = lastConnectedAt,
            LastDisconnectedAt = lastDisconnectedAt,
            IsClosed = isClosed
        }];

    /// <inheritdoc />
    public void SetClosed(PeerConnectionKind kind, string userName, bool closed)
    {
        if (kind != PeerConnectionKind.Server || transport is null || serverEndpoint is null || serverLink is null || isClosed == closed) { return; }

        isClosed = closed;
        transport.SetClosed(serverEndpoint, closed);
        if (closed)
        {
            serverLink.Close();
            DropInbound();
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
        if (kind != PeerConnectionKind.Server || transport is null || serverEndpoint is null || serverLink is null || isClosed) { return; }

        transport.Reset(serverEndpoint);
        DropInbound();
        serverLink.Refresh();
    }

    private void DropInbound()
    {
        foreach (PeerConnection connection in inboundConnections.Keys) { connection.Drop(); }
    }

    private async Task<bool> SendOnce(string messageId, object message, CancellationToken cancellation)
    {
        if (transport is null || serverEndpoint is null || isClosed) { return false; }

        try
        {
            using OwnedBuffer buf = PeerSerializer.Serialize(message);
            return await transport.Request(serverEndpoint, buf.Memory, new PeerSendOptions { Priority = engineController.GetPriority(message) }, cancellation);
        }
        catch
        {
            return false;
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

    private void OnConnected(PeerConnectionEventArgs args)
    {
        if (args.Connection.IsInbound)
        {
            if (isClosed)
            {
                args.Connection.Drop();
                return;
            }

            inboundConnections[args.Connection] = 0;
        }

        if (!IsServerConnection(args.Connection)) { return; }

        if (args.Connection.IdentitySubject is { } subject) { serverName = PeerIdentity.ExtractCommonName(subject); }

        // An IP connection only counts as up once a heartbeat is acknowledged (see OnHeartbeatAcknowledged); a serial
        // link is cabled to exactly one node and only ever comes up when that node answers, so it is up immediately.
        if (args.Connection.Endpoint is { IsSerial: true })
        {
            UpdateConnectionStatus(true);
        }
    }

    private void OnHeartbeatAcknowledged()
    {
        if (!isClosed) { UpdateConnectionStatus(true); }
    }

    private void OnDisconnected(PeerConnectionEventArgs args)
    {
        inboundConnections.TryRemove(args.Connection, out _);
        if (IsServerConnection(args.Connection)) { UpdateConnectionStatus(false); }
    }

    private void UpdateConnectionStatus(bool connected)
    {
        if (isConnected == connected) { return; }

        isConnected = connected;
        if (connected)
        {
            lastConnectedAt = DateTime.UtcNow;
            logger.LogInformation("Connected to server");
        }
        else
        {
            lastDisconnectedAt = DateTime.UtcNow;
            logger.LogWarning("Server unreachable");
        }
        StatusesChanged?.Invoke();
    }

    private bool IsServerConnection(PeerConnection connection)
        => serverEndpoint is { } expected && expected.Equals(connection.Endpoint);

    private void OnReceived(PeerReceivedEventArgs args)
        => _ = Task.Run(() => HandleMessage(args.Payload));

    internal Task<bool> HandleMessage(ReadOnlyMemory<byte> data)
        => PeerMessageDispatcher.Dispatch(data, engineController, logger, MessageDelivered, ConfirmationReceived);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (transport is not null) { await transport.DisposeAsync(); }
    }
}
