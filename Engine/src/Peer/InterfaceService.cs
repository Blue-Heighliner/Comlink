namespace BlueHeighliner.Comlink.Engine.Peer;

/// <summary>
/// Hosts the local interface listener: an MSMT connection that behaves like a peer connection — same
/// transport, same message type (<see cref="IEngineController.MessageType"/>) — but represents no user of
/// its own. Every message an interface sends is routed out to other peers as if this user had originated
/// it itself.
/// </summary>
/// <remarks>
/// Mirroring an inbound peer message back out to a connected interface is not currently implemented:
/// MSMT's client-request/server-response model means a connection an interface client initiated can only
/// ever be used to acknowledge what that client sends, never to push a new message back down it, so an
/// interface tool would need to run its own MSMT receiver for this instance to dial back into - a
/// materially different integration shape than "open a socket and read" that is not yet provided. See
/// <c>Docs/Interface.md</c>.
/// </remarks>
internal interface IInterfaceService : IAsyncDisposable
{
    /// <summary>Starts the inbound interface listener and blocks until <paramref name="cancellation"/> is cancelled.</summary>
    Task Start(CancellationToken cancellation);
}

/// <inheritdoc cref="IInterfaceService" />
internal sealed class InterfaceService : IInterfaceService
{
    /// <summary>Initializes a new <see cref="InterfaceService"/>.</summary>
    public InterfaceService(
        IMsmtPeerFactory peerFactory,
        IEngineController engineController,
        IMessageRoutingService routingService,
        IUserService userService,
        ILoggerFactory loggerFactory)
    {
        this.peerFactory = peerFactory;
        this.engineController = engineController;
        this.routingService = routingService;
        this.userService = userService;
        logger = loggerFactory.CreateLogger("ACTIVITY");
    }

    private readonly IMsmtPeerFactory peerFactory;
    private readonly IEngineController engineController;
    private readonly IMessageRoutingService routingService;
    private readonly IUserService userService;
    private readonly ILogger logger;

    private IMsmtPeer? peer;

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        MsmtOptions options;
        try
        {
            options = engineController.ConnectionOptions;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError("Interface listener cannot start: {Message}", ex.Message);
            return;
        }

        peer = peerFactory.Create(options);
        peer.Received.Subscribe(OnReceived);
        peer.StartListener(engineController.InterfacePort, "127.0.0.1");

        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
    }

    private void OnReceived(MsmtReceivedEventArgs args)
    {
        byte[] copy;
        using (args.Payload) { copy = args.Payload.Memory.ToArray(); }
        _ = Task.Run(() => HandleInterfaceMessage(copy));
    }

    internal async Task HandleInterfaceMessage(ReadOnlyMemory<byte> data)
    {
        object? message;
        try
        {
            message = PeerSerializer.Deserialize(engineController.MessageType, data);
        }
        catch
        {
            return;
        }
        if (message is null) { return; }

        UserInfo? userInfo = userService.GetCurrentUserInfo();
        if (userInfo is null) { return; }

        SendMessagePayload payload = new()
        {
            Subject = engineController.GetSubject(message),
            Body = engineController.GetBody(message),
            Addresses = engineController.GetAddresses(message).Select(a => new AddressPayload { UserName = a.UserName, Type = a.Type.ToString() }).ToList(),
            IsAlert = engineController.GetIsAlert(message),
            Priority = engineController.GetPriority(message),
            Tag = engineController.GetTag(message)
        };

        await routingService.Route(userInfo.Name, payload, CancellationToken.None);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (peer is not null) { await peer.DisposeAsync(); }
    }
}
