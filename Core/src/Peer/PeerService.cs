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
/// Implements <see cref="IPeerService"/> for <see cref="NodeRole.Peer"/> by wrapping an <see cref="IPeerTransport"/>.
/// Traffic carries an instance of <see cref="IEngineController.MessageType"/> directly with no envelope; delivery
/// confirmation is derived from the transport's own acknowledgement of the send, not from an application-level
/// reply. Each user's <see cref="IEngineController.GetEndpoint"/> decides whether they are reached over IP or serial.
/// </summary>
internal sealed class PeerService : IPeerService, IAsyncDisposable
{
    /// <summary>Initializes a new <see cref="PeerService"/>, deferring its <see cref="IPeerTransport"/> to <see cref="Start"/> once a current user is registered.</summary>
    public PeerService(
        IPeerTransportFactory transportFactory,
        IEngineController engineController,
        ILoggerFactory loggerFactory)
    {
        this.transportFactory = transportFactory;
        this.engineController = engineController;
        logger = loggerFactory.CreateLogger("ACTIVITY");
    }

    /// <summary>Initializes a <see cref="PeerService"/> with a pre-built transport; intended for unit testing.</summary>
    internal PeerService(IPeerTransport transport, IEngineController engineController, ILoggerFactory loggerFactory)
    {
        transportFactory = null;
        this.engineController = engineController;
        logger = loggerFactory.CreateLogger("ACTIVITY");
        Wire(transport);
    }

    private readonly IPeerTransportFactory? transportFactory;
    private readonly IEngineController engineController;
    private readonly ILogger logger;

    private IPeerTransport? transport;

    /// <inheritdoc />
    public event Func<object, Task>? MessageDelivered;
    /// <inheritdoc />
    public event Func<string, string, Task>? ConfirmationReceived;
    /// <inheritdoc />
    public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        Wire(transportFactory!.Create());
        transport!.StartListener(engineController.PeerPort);
        foreach (string userName in engineController.Users)
        {
            if (engineController.GetEndpoint(userName) is { IsSerial: true } endpoint)
            {
                transport.Open(endpoint);
            }
        }

        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
    }

    /// <inheritdoc />
    public async Task<bool> Send(string userName, object message, CancellationToken cancellation = default)
    {
        if (transport is null) { return false; }

        UserEndpoint? endpoint = engineController.GetEndpoint(userName);
        if (endpoint is null) { return false; }

        DeliveryTag tag = new(engineController.GetMessageId(message), userName);
        try
        {
            using OwnedBuffer buf = PeerSerializer.Serialize(message);
            bool accepted = await transport.Request(
                endpoint,
                buf.Memory,
                new PeerSendOptions { Priority = engineController.GetPriority(message), Transmitted = () => RaiseDeliveryStatusChanged(tag, DestinationStatus.Sent) },
                cancellation);

            RaiseDeliveryStatusChanged(tag, accepted ? DestinationStatus.Confirmed : DestinationStatus.Failed);
            return accepted;
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

    private void Wire(IPeerTransport newTransport)
    {
        transport = newTransport;
        newTransport.Received.Listen(OnReceived);
    }

    private void OnReceived(PeerReceivedEventArgs args)
        => _ = Task.Run(() => HandleMessage(args.Payload));

    internal Task<bool> HandleMessage(ReadOnlyMemory<byte> data)
        => PeerMessageDispatcher.Dispatch(data, engineController, logger, MessageDelivered, ConfirmationReceived);

    private void RaiseDeliveryStatusChanged(DeliveryTag tag, DestinationStatus status)
    {
        if (DeliveryStatusChanged is null) { return; }
        _ = Task.Run(() => DeliveryStatusChanged(tag.MessageId, tag.UserName, status));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => transport?.DisposeAsync() ?? ValueTask.CompletedTask;

    private sealed record DeliveryTag(string MessageId, string UserName);
}
