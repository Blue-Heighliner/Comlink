namespace BlueHeighliner.Comlink;

/// <summary>
/// Hosts the local interface listener: an MSMT connection that behaves like a peer connection — same
/// transport, same frame type (<see cref="IEngineController.FrameType"/>) — but represents no user of
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
    /// <summary>Closes the listener and opens it again from the configuration as it is now (its port and certificates). Does nothing before <see cref="Start"/>.</summary>
    void Restart();

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
        logger = loggerFactory.CreateLogger(LogCategories.App);
    }

    private readonly IMsmtSessionPeer.IFactory peerFactory;
    private readonly IEngineController engineController;
    private readonly IMessageRoutingService routingService;
    private readonly IUserService userService;
    private readonly ILogger logger;

    private readonly Lock runLock = new();
    private IMsmtSessionPeer? peer;
    private CancellationTokenSource? current;
    private bool restartRequested;

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        while (true)
        {
            using CancellationTokenSource run = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            lock (runLock)
            {
                restartRequested = false;
                current = run;
            }

            await Listen(run.Token);

            lock (runLock)
            {
                if (!restartRequested || cancellation.IsCancellationRequested) { return; }
            }
        }
    }

    /// <inheritdoc />
    public void Restart()
    {
        lock (runLock)
        {
            if (current is null) { return; }

            restartRequested = true;
            current.Cancel();
        }
    }

    private async Task Listen(CancellationToken cancellation)
    {
        MsmtSessionPeerOptions options;
        try
        {
            options = engineController.ConnectionOptions;
        }
        catch (InvalidOperationException ex) when (ex is not InvalidEngineConfigurationException)
        {
            logger.Record(LogEvents.InterfaceCannotStart, "Interface listener cannot start: {Message}", ex.Message);
            logger.Record(LogEvents.InterfaceNotWorking, "The interface for other applications is not working");
            await WaitForRestart(cancellation);
            return;
        }

        IMsmtSessionPeer listener = peerFactory.Create(options);
        peer = listener;
        listener.Receiver = OnReceived;
        listener.StartListener(engineController.InterfacePort, "127.0.0.1");

        await WaitForRestart(cancellation);
        await listener.DisposeAsync();
        if (ReferenceEquals(peer, listener)) { peer = null; }
    }

    private static async Task WaitForRestart(CancellationToken cancellation)
    {
        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
    }

    private void OnReceived(IMsmtConnection connection, IMemoryOwner<byte> payload, IMsmtResponder? responder)
    {
        byte[] copy;
        using (payload) { copy = payload.Memory.ToArray(); }
        _ = Task.Run(() => HandleInterfaceMessage(copy));
        responder?.Accept(ReadOnlyMemory<byte>.Empty);
    }

    internal async Task HandleInterfaceMessage(ReadOnlyMemory<byte> data)
    {
        object message;
        try
        {
            message = engineController.FrameSerializer.Deserialize(data, null);
        }
        catch
        {
            return;
        }

        // FrameSerializer determines the type from the data itself, so bytes from an incompatible sender
        // could describe a type other than this instance's own FrameType; treat that the same as a
        // failed deserialize rather than let a mismatched cast below throw.
        if (message.GetType() != engineController.FrameType) { return; }

        UserInfo? userInfo = userService.GetCurrentUserInfo();
        if (userInfo is null) { return; }

        if (engineController.GetUnconfiguredLevelReason(message) is { } invalid)
        {
            logger.Record(LogEvents.InvalidMessage, "A message from {Source} is invalid and was dropped: it {Reason}", "the interface", invalid);
            logger.Record(LogEvents.MessageDropped, "A message from {Source} was dropped because it was not valid", "another application");
            return;
        }

        SendMessagePayload payload = new()
        {
            Body = engineController.GetBody(message),
            Addresses = engineController.GetAddresses(message).Select(a => new AddressPayload { UserName = a.UserName, Type = a.Type.ToString(), Information = a.Information }).ToList(),
            Priority = engineController.GetMessagePriority(message),
            Tag = engineController.GetTag(message),
            MessageLevel = engineController.GetMessageLevel(message)
        };

        try
        {
            await routingService.Route(userInfo.Name, payload, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.Record(LogEvents.RouteFailed, ex, "Failed to {Action}", "route a message received on the interface");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (peer is not null) { await peer.DisposeAsync(); }
    }
}
