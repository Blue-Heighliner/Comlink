namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// Hosts the local interface listener: an MSMT connection that behaves like a peer connection — same
/// transport, same message type (<see cref="IEngineController.MessageType"/>) — but represents no user of
/// its own. Every message an interface sends is routed out to other peers as if this user had originated
/// it itself.
/// </summary>
/// <remarks>
/// Mirroring an inbound peer message back out to a connected interface is not currently implemented: doing
/// so would need that interface client's connection kept open and correlated to its own inbound peer
/// traffic, rather than treated as a one-way injection point, and this instance never writes back down a
/// connection a remote party opened to it in the first place - see <c>Docs/Components/MsmtIntegration.md</c>.
/// An interface tool would instead need to run its own MSMT listener for this instance to dial back into,
/// a materially different integration shape than "open a socket and read" that is not yet provided. See
/// <c>Docs/Components/Interface.md</c>.
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
        IMsmtSessionPeer.IFactory peerFactory,
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

    private readonly IMsmtSessionPeer.IFactory peerFactory;
    private readonly IEngineController engineController;
    private readonly IMessageRoutingService routingService;
    private readonly IUserService userService;
    private readonly ILogger logger;

    private IMsmtSessionPeer? peer;

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        MsmtSessionPeerOptions options;
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
        peer.Receiver = OnReceived;
        peer.StartListener(engineController.InterfacePort, "127.0.0.1");

        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
    }

    private ValueTask<MsmtReceiveResult?> OnReceived(IMsmtConnection connection, ReadOnlyMemory<byte> payload, bool isResponseRequested)
    {
        byte[] copy = payload.ToArray();
        _ = Task.Run(() => HandleInterfaceMessage(copy));
        return ValueTask.FromResult<MsmtReceiveResult?>(isResponseRequested ? MsmtReceiveResult.Accept() : null);
    }

    internal async Task HandleInterfaceMessage(ReadOnlyMemory<byte> data)
    {
        object? message;
        try
        {
            message = engineController.NetworkSerializer.Deserialize(data);
        }
        catch
        {
            return;
        }

        // NetworkSerializer determines the type from the data itself, so bytes from an incompatible sender
        // could describe a type other than this instance's own MessageType; treat that the same as a
        // failed deserialize rather than let a mismatched cast below throw.
        if (message is null || message.GetType() != engineController.MessageType) { return; }

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
