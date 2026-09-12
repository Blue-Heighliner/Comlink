namespace BlueHeighliner.Comlink.Engine.Peer;

/// <summary>
/// Implements <see cref="IPeerService"/> for <see cref="NodeRole.Client"/>: sends every outbound message
/// to the configured server (<see cref="IEngineController"/>), regardless of addressee — the server
/// performs the actual user-to-connection routing. Also runs its own MSMT receiver on <see
/// cref="IEngineController.PeerPort"/> so the server can deliver messages back to this client - MSMT's
/// client-request/server-response model means the server can never push over a connection this client
/// initiated, so a genuinely separate connection, dialed by the server back to this client, carries that
/// direction instead. A background <see cref="MsmtConnectionMonitor"/> proactively opens and maintains a
/// connection to the server with a recurring heartbeat, independent of whether any real message is being
/// sent, so <see cref="GetStatuses"/> reflects the connection's live state continuously rather than only the
/// moment a message last happened to flow. See <c>Docs/Peer.md</c>.
/// </summary>
internal sealed class ClientPeerService : IPeerService, IConnectionStatusService, IAsyncDisposable
{
    /// <summary>Initializes a new <see cref="ClientPeerService"/>.</summary>
    public ClientPeerService(
        IMsmtPeerFactory peerFactory,
        IEngineController engineController,
        ILoggerFactory loggerFactory)
    {
        this.peerFactory = peerFactory;
        this.engineController = engineController;
        logger = loggerFactory.CreateLogger("ACTIVITY");
    }

    private readonly IMsmtPeerFactory peerFactory;
    private readonly IEngineController engineController;
    private readonly ILogger logger;
    private readonly MsmtConnectionMonitor connectionMonitor = new();

    private readonly ConcurrentDictionary<string, Task<bool>> inFlightSends = new();

    private IMsmtPeer? peer;
    private MsmtTarget? serverTarget;
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

        MsmtOptions options;
        try
        {
            options = engineController.ConnectionOptions;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError("Client role cannot start: {Message}", ex.Message);
            return;
        }

        serverTarget = new MsmtTarget { Host = endpoint.IpAddress, Port = endpoint.Port };
        peer = peerFactory.Create(options);
        peer.Connected.Subscribe(OnConnected);
        peer.Disconnected.Subscribe(OnDisconnected);
        peer.Received.Subscribe(OnReceived);
        peer.StartListener(engineController.PeerPort);
        logger.LogInformation("Client peer listening for server-originated deliveries");
        connectionMonitor.Maintain(peer, serverTarget, cancellation);

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
            UserName = string.Empty,
            Kind = PeerConnectionKind.Server,
            IsConnected = isConnected,
            LastConnectedAt = lastConnectedAt,
            LastDisconnectedAt = lastDisconnectedAt
        }];

    private async Task<bool> SendOnce(string messageId, object message, CancellationToken cancellation)
    {
        if (peer is null || serverTarget is null) { return false; }

        try
        {
            using OwnedBuffer buf = PeerSerializer.Serialize(message);
            MsmtResponse response = await peer.Request(serverTarget, buf.Memory, new MsmtSendOptions { Priority = engineController.GetPriority(message), Tag = messageId }, cancellation);
            response.Payload.Dispose();
            return response.Success;
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

    private void OnConnected(MsmtConnectedEventArgs args)
    {
        if (IsServerTarget(args.Connection.Target)) { UpdateConnectionStatus(true); }
    }

    private void OnDisconnected(MsmtDisconnectedEventArgs args)
    {
        if (IsServerTarget(args.Connection.Target)) { UpdateConnectionStatus(false); }
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

    private bool IsServerTarget(MsmtTarget target)
        => serverTarget is { } expected && target.Host == expected.Host && target.Port == expected.Port;

    private void OnReceived(MsmtReceivedEventArgs args)
    {
        byte[] copy;
        using (args.Payload) { copy = args.Payload.Memory.ToArray(); }
        _ = Task.Run(() => HandleMessage(copy));
    }

    internal Task<bool> HandleMessage(ReadOnlyMemory<byte> data)
        => PeerMessageDispatcher.Dispatch(data, engineController, logger, MessageDelivered, ConfirmationReceived);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (peer is not null) { await peer.DisposeAsync(); }
    }
}
