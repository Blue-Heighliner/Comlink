namespace BlueHeighliner.Comlink.Peer;

/// <summary>Manages inbound and outbound peer connections and exposes Engine-level delivery events.</summary>
internal interface IPeerService
{
    /// <summary>Raised when a remote user delivers a new (non-confirmation) message to this user.</summary>
    event Func<object, Task>? MessageDelivered;
    /// <summary>
    /// Raised when a remote user delivers a user-read confirmation message instead of an ordinary
    /// message (<see cref="IEngineController.GetConfirmationMessageId"/> is non-empty). Carries the ID of
    /// the message being confirmed and the confirming user's name; not raised via <see cref="MessageDelivered"/>.
    /// </summary>
    event Func<string, string, Task>? ConfirmationReceived;
    /// <summary>Raised whenever the delivery status of a message sent to a specific user changes.</summary>
    event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;
    /// <summary>Starts the inbound peer listener and blocks until <paramref name="cancellation"/> is cancelled.</summary>
    Task Start(CancellationToken cancellation);
    /// <summary>Sends <paramref name="message"/> (an instance of <see cref="IEngineController.MessageType"/>) to the peer identified by <paramref name="userName"/>.</summary>
    Task<bool> Send(string userName, object message, CancellationToken cancellation = default);
    /// <summary>Raises <see cref="MessageDelivered"/> directly with <paramref name="payload"/>, without a network round-trip. Used when a user sends a message to itself.</summary>
    Task DeliverLocal(object payload);
}

/// <summary>
/// Implements <see cref="IPeerService"/> by wrapping an <see cref="IMsmtPeer"/>. Traffic carries an
/// instance of <see cref="IEngineController.MessageType"/> directly with no envelope; delivery confirmation
/// is derived from MSMT's own <see cref="IMsmtPeer.Request"/> outcome for the send's tag, not from an
/// application-level acknowledgement.
/// </summary>
internal sealed class PeerService : IPeerService, IAsyncDisposable
{
    /// <summary>Initializes a new <see cref="PeerService"/>, deferring its <see cref="IMsmtPeer"/> to <see cref="Start"/> once a current user is registered.</summary>
    public PeerService(
        IMsmtPeerFactory peerFactory,
        IEngineController engineController,
        ILoggerFactory loggerFactory)
    {
        this.peerFactory = peerFactory;
        this.engineController = engineController;
        logger = loggerFactory.CreateLogger("ACTIVITY");
    }

    /// <summary>Initializes a <see cref="PeerService"/> with a pre-built peer; intended for unit testing.</summary>
    internal PeerService(IMsmtPeer peer, IEngineController engineController, ILoggerFactory loggerFactory)
    {
        peerFactory = null;
        this.engineController = engineController;
        logger = loggerFactory.CreateLogger("ACTIVITY");
        Wire(peer);
    }

    private readonly IMsmtPeerFactory? peerFactory;
    private readonly IEngineController engineController;
    private readonly ILogger logger;

    private IMsmtPeer? peer;

    /// <inheritdoc />
    public event Func<object, Task>? MessageDelivered;
    /// <inheritdoc />
    public event Func<string, string, Task>? ConfirmationReceived;
    /// <inheritdoc />
    public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;

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
            logger.LogError("Peer role cannot start: {Message}", ex.Message);
            return;
        }

        Wire(peerFactory!.Create(options));
        peer!.StartListener(engineController.PeerPort);

        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
    }

    /// <inheritdoc />
    public async Task<bool> Send(string userName, object message, CancellationToken cancellation = default)
    {
        if (peer is null) { return false; }

        UserEndpoint? endpoint = engineController.GetEndpoint(userName);
        if (endpoint is null) { return false; }

        DeliveryTag tag = new(engineController.GetMessageId(message), userName);
        try
        {
            using OwnedBuffer buf = PeerSerializer.Serialize(message);
            MsmtResponse response = await peer.Request(
                new MsmtTarget { Host = endpoint.IpAddress, Port = endpoint.Port },
                buf.Memory,
                new MsmtSendOptions { Priority = engineController.GetPriority(message), Tag = tag },
                cancellation);
            response.Payload.Dispose();

            RaiseDeliveryStatusChanged(tag, response.Success ? DestinationStatus.Confirmed : DestinationStatus.Failed);
            return response.Success;
        }
        catch
        {
            RaiseDeliveryStatusChanged(tag, DestinationStatus.Failed);
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

    private void Wire(IMsmtPeer newPeer)
    {
        peer = newPeer;
        newPeer.Received.Subscribe(OnReceived);
        newPeer.PackageChanged.Subscribe(OnPackageChanged);
    }

    private void OnReceived(MsmtReceivedEventArgs args)
    {
        byte[] copy;
        using (args.Payload) { copy = args.Payload.Memory.ToArray(); }
        _ = Task.Run(() => HandleMessage(copy));
    }

    internal Task<bool> HandleMessage(ReadOnlyMemory<byte> data)
        => PeerMessageDispatcher.Dispatch(data, engineController, logger, MessageDelivered, ConfirmationReceived);

    private void OnPackageChanged(MsmtPackageChangedEventArgs args)
    {
        if (args.Package.Tag is DeliveryTag tag && args.Status == MsmtSendStatus.PendingAcknowledgement)
        {
            RaiseDeliveryStatusChanged(tag, DestinationStatus.Sent);
        }
    }

    private void RaiseDeliveryStatusChanged(DeliveryTag tag, DestinationStatus status)
    {
        if (DeliveryStatusChanged is null) { return; }
        _ = Task.Run(() => DeliveryStatusChanged(tag.MessageId, tag.UserName, status));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => peer?.DisposeAsync() ?? ValueTask.CompletedTask;

    private sealed record DeliveryTag(string MessageId, string UserName);
}
