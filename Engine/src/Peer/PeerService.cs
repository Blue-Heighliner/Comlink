namespace BlueHeighliner.Comlink.Engine.Peer;

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
    /// <summary>Raised whenever the OFT delivery status of a message sent to a specific user changes.</summary>
    event Func<string, string, OftDeliveryStatus, Task>? DeliveryStatusChanged;
    /// <summary>Starts the inbound peer listener and blocks until <paramref name="cancellation"/> is cancelled.</summary>
    Task Start(CancellationToken cancellation);
    /// <summary>Sends <paramref name="message"/> (an instance of <see cref="IEngineController.MessageType"/>) to the peer identified by <paramref name="userName"/>.</summary>
    Task<bool> Send(string userName, object message, CancellationToken cancellation = default);
    /// <summary>Raises <see cref="MessageDelivered"/> directly with <paramref name="payload"/>, without a network round-trip. Used when a user sends a message to itself.</summary>
    Task DeliverLocal(object payload);
}

/// <summary>
/// Implements <see cref="IPeerService"/> by wrapping an <see cref="IOftPeer"/>. Traffic carries an
/// instance of <see cref="IEngineController.MessageType"/> directly with no envelope; delivery confirmation
/// is derived entirely from OFT's own <see cref="OftDeliveryStatus"/> stream, not from an
/// application-level acknowledgement.
/// </summary>
internal sealed class PeerService : IPeerService, IAsyncDisposable
{
    /// <summary>Initializes a new <see cref="PeerService"/> and wires up an <see cref="IOftPeer"/> using Engine infrastructure.</summary>
    public PeerService(
        IOftPeerFactory peerFactory,
        IEngineController engineController,
        ILoggerFactory loggerFactory)
    {
        this.engineController = engineController;
        logger = loggerFactory.CreateLogger("ACTIVITY");
        peer = peerFactory.Create(engineController.ConnectionOptions);
        peer.ReceivedHandler = OnReceived;
        peer.DeliveryStatusHandler = OnDeliveryStatus;
    }

    /// <summary>Initializes a <see cref="PeerService"/> with a pre-built peer; intended for unit testing.</summary>
    internal PeerService(IOftPeer peer, IEngineController engineController, ILoggerFactory loggerFactory)
    {
        this.peer = peer;
        this.engineController = engineController;
        logger = loggerFactory.CreateLogger("ACTIVITY");
        peer.ReceivedHandler = OnReceived;
        peer.DeliveryStatusHandler = OnDeliveryStatus;
    }

    private readonly IOftPeer peer;
    private readonly IEngineController engineController;
    private readonly ILogger logger;

    /// <inheritdoc />
    public event Func<object, Task>? MessageDelivered;
    /// <inheritdoc />
    public event Func<string, string, Task>? ConfirmationReceived;
    /// <inheritdoc />
    public event Func<string, string, OftDeliveryStatus, Task>? DeliveryStatusChanged;

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        await peer.Listen(new IPEndPoint(IPAddress.Any, engineController.PeerPort), cancellation);
        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
    }

    /// <inheritdoc />
    public async Task<bool> Send(string userName, object message, CancellationToken cancellation = default)
    {
        UserEndpoint? endpoint = engineController.GetEndpoint(userName);
        if (endpoint is null) { return false; }

        using OwnedBuffer buf = PeerSerializer.Serialize(message);
        try
        {
            await peer.Send(endpoint.IpAddress, endpoint.Port, buf.Memory,
                priority: engineController.GetPriority(message),
                tag: new DeliveryTag(engineController.GetMessageId(message), userName),
                cancellationToken: cancellation);
            return true;
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

    private void OnReceived(OftIdentity identity, IMemoryOwner<byte> data)
    {
        byte[] copy;
        using (data) { copy = data.Memory.ToArray(); }
        _ = Task.Run(() => HandleMessage(copy));
    }

    internal Task<bool> HandleMessage(ReadOnlyMemory<byte> data)
        => PeerMessageDispatcher.Dispatch(data, engineController, logger, MessageDelivered, ConfirmationReceived);

    private void OnDeliveryStatus(object tag, OftDeliveryStatus status)
    {
        if (tag is not DeliveryTag deliveryTag || DeliveryStatusChanged is null) { return; }
        _ = Task.Run(() => DeliveryStatusChanged(deliveryTag.MessageId, deliveryTag.UserName, status));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => peer.DisposeAsync();

    private sealed record DeliveryTag(string MessageId, string UserName);
}
