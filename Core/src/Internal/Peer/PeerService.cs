namespace BlueHeighliner.Comlink.Peer;

/// <summary>Manages inbound and outbound peer connections and exposes Engine-level delivery events.</summary>
internal interface IPeerService
{
    /// <summary>Raised when a remote user delivers a new (non-confirmation) message to this user.</summary>
    event Func<object, Task>? FrameDelivered;
    /// <summary>
    /// Raised when a remote user delivers a user-read confirmation message instead of an ordinary
    /// message (<see cref="IEngineController.GetConfirmationMessageId"/> is non-empty). Carries the ID of
    /// the message being confirmed and the confirming user's name; not raised via <see cref="FrameDelivered"/>.
    /// </summary>
    event Func<string, string, Task>? ConfirmationReceived;
    /// <summary>Raised whenever the delivery status of a message sent to a specific user changes.</summary>
    event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;
    /// <summary>Raised when a user goes from having no live connection to having at least one.</summary>
    event Func<string, Task>? UserConnected;
    /// <summary>Raised when a user goes from having at least one live connection to having none.</summary>
    event Func<string, Task>? UserDisconnected;
    /// <summary>Returns the names of every user currently reachable over at least one live connection.</summary>
    IReadOnlyList<string> GetConnectedUsers();
    /// <summary>Whether <paramref name="userName"/> is currently reachable over at least one live connection, without allocating the full list <see cref="GetConnectedUsers"/> would.</summary>
    bool IsUserConnected(string userName);
    /// <summary>Starts the inbound peer listener and blocks until <paramref name="cancellation"/> is cancelled.</summary>
    Task Start(CancellationToken cancellation);
    /// <summary>Sends <paramref name="message"/> (an instance of <see cref="IEngineController.FrameType"/>) to the peer identified by <paramref name="userName"/>.</summary>
    Task<bool> Send(string userName, object message, CancellationToken cancellation = default);
    /// <summary>
    /// Sends <paramref name="packet"/> (an instance of <see cref="IEngineController.PacketType"/>) directly to the
    /// peer identified by <paramref name="userName"/>, serialized via <see cref="IEngineController.PacketSerializer"/>
    /// instead of <see cref="IEngineController.NetworkSerializer"/> - bypassing the normal packetization/reassembly a
    /// full message goes through, and carrying no delivery-status tracking of its own.
    /// </summary>
    Task<bool> SendPacket(string userName, object packet, CancellationToken cancellation = default);
    /// <summary>Raises <see cref="FrameDelivered"/> directly with <paramref name="payload"/>, without a network round-trip. Used when a user sends a message to itself.</summary>
    Task DeliverLocal(object payload);
}

/// <summary>
/// Implements <see cref="IPeerService"/> for <see cref="UserRole.Peer"/> by wrapping an <see cref="IPeerTransport"/>.
/// Traffic carries an instance of <see cref="IEngineController.FrameType"/> directly with no envelope; delivery
/// confirmation is derived from the transport's own acknowledgement of the send, not from an application-level
/// reply. The node listens on <see cref="IEngineController.PeerPort"/> and keeps a connection open to each of its
/// <see cref="IEngineController.OutgoingPoints"/>; which user is behind a connection is worked out when it forms, and a
/// message for a user goes over whichever connection is currently identified as them, in whichever direction it was
/// opened.
/// </summary>
internal sealed class PeerService : IPeerService, IReconfigurable, IAsyncDisposable
{
    /// <summary>Initializes a new <see cref="PeerService"/>, deferring its <see cref="IPeerTransport"/> to <see cref="Start"/> once a current user is registered.</summary>
    public PeerService(
        IPeerTransportFactory transportFactory,
        IEngineController engineController,
        ILoggerFactory loggerFactory)
    {
        this.transportFactory = transportFactory;
        this.engineController = engineController;
        points = new PointMaintenance(new PeerConnectionMonitor(engineController));
        logger = loggerFactory.CreateLogger("ACTIVITY");
    }

    /// <summary>Initializes a <see cref="PeerService"/> with a pre-built transport; intended for unit testing.</summary>
    internal PeerService(IPeerTransport transport, IEngineController engineController, ILoggerFactory loggerFactory)
    {
        transportFactory = null;
        this.engineController = engineController;
        points = new PointMaintenance(new PeerConnectionMonitor(engineController));
        logger = loggerFactory.CreateLogger("ACTIVITY");
        Wire(transport);
    }

    private readonly IPeerTransportFactory? transportFactory;
    private readonly IEngineController engineController;
    private readonly ILogger logger;
    private readonly PointMaintenance points;
    private readonly Lock reconfigureLock = new();
    private readonly UserConnections connections = new();

    private IPeerTransport? transport;
    private CancellationToken lifetime;
    private int listenPort;
    private int disposed;

    /// <inheritdoc />
    public event Func<object, Task>? FrameDelivered;
    /// <inheritdoc />
    public event Func<string, string, Task>? ConfirmationReceived;
    /// <inheritdoc />
    public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;
    /// <inheritdoc />
    public event Func<string, Task>? UserConnected;
    /// <inheritdoc />
    public event Func<string, Task>? UserDisconnected;

    /// <inheritdoc />
    public IReadOnlyList<string> GetConnectedUsers() => connections.GetUsers();

    /// <inheritdoc />
    public bool IsUserConnected(string userName) => connections.Has(userName);

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        Wire(transportFactory!.Create());
        lock (reconfigureLock)
        {
            lifetime = cancellation;
            listenPort = engineController.PeerPort;
            transport!.StartListener(listenPort);
            points.Sync(transport, engineController.OutgoingPoints, lifetime);
        }

        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
    }

    /// <inheritdoc />
    public void Reconfigure()
    {
        lock (reconfigureLock)
        {
            if (transport is null || lifetime == default) { return; }

            if (engineController.PeerPort != listenPort)
            {
                transport.StopListener();
                listenPort = engineController.PeerPort;
                transport.StartListener(listenPort);
            }

            points.Sync(transport, engineController.OutgoingPoints, lifetime);
        }
    }

    /// <inheritdoc />
    public async Task<bool> Send(string userName, object message, CancellationToken cancellation = default)
    {
        if (transport is null) { return false; }

        DeliveryTag tag = new(engineController.GetFrameId(message), userName);
        if (connections.Get(userName) is not { } connection)
        {
            logger.LogWarning("{MessageId} cannot be sent to {User}: no connection is identified as them", tag.MessageId, userName);
            RaiseDeliveryStatusChanged(tag, DestinationStatus.Failed);
            return false;
        }

        try
        {
            using IMemoryOwner<byte> buf = engineController.NetworkSerializer.Serialize(message);
            bool accepted = await transport.Request(
                connection,
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
    public async Task<bool> SendPacket(string userName, object packet, CancellationToken cancellation = default)
    {
        if (transport is null || connections.Get(userName) is not { } connection) { return false; }

        try
        {
            using IMemoryOwner<byte> buf = engineController.PacketSerializer!.Serialize(packet);
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

    private void Wire(IPeerTransport newTransport)
    {
        transport = newTransport;
        newTransport.Received.Listen(OnReceived);
        newTransport.Connected.Listen(args => OnConnected(args.Connection));
        newTransport.Disconnected.Listen(args => OnDisconnected(args.Connection));
    }

    private void OnConnected(PeerConnection connection)
    {
        if (connection.User is not { } user) { return; }

        if (connections.AddNewlyOnline(connection)) { PeerConnectionNotifier.Raise(UserConnected, user.Name, "connecting", logger); }
    }

    private void OnDisconnected(PeerConnection connection)
    {
        (string? userName, bool nowOffline) = connections.RemoveNowOffline(connection);
        if (nowOffline && userName is not null) { PeerConnectionNotifier.Raise(UserDisconnected, userName, "disconnecting", logger); }
    }

    private void OnReceived(PeerReceivedEventArgs args)
        => _ = Task.Run(() => HandleMessage(args.Payload));

    internal Task<bool> HandleMessage(ReadOnlyMemory<byte> data)
        => PeerFrameDispatcher.Dispatch(data, engineController, logger, FrameDelivered, ConfirmationReceived);

    private void RaiseDeliveryStatusChanged(DeliveryTag tag, DestinationStatus status)
    {
        if (DeliveryStatusChanged is null) { return; }
        _ = Task.Run(async () =>
        {
            try { await DeliveryStatusChanged.InvokeAll(tag.MessageId, tag.UserName, status); }
            catch (Exception ex) { logger.LogError(ex, "Failed to handle the delivery status of {MessageId} to {UserName}", tag.MessageId, tag.UserName); }
        });
    }


    /// <inheritdoc />
    public ValueTask DisposeAsync()
        => Interlocked.Exchange(ref disposed, 1) == 0 && transport is not null ? transport.DisposeAsync() : ValueTask.CompletedTask;

    private sealed record DeliveryTag(string MessageId, string UserName);
}
