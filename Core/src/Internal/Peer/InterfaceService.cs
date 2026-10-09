namespace BlueHeighliner.Comlink;

/// <summary>
/// Hosts the local interface listener: an MSMT connection that behaves like a peer connection — same
/// transport, same frame type (<see cref="IEngineController.FrameType"/>) — but represents no user of
/// its own. Every frame an interface sends is handed to the host's frame handler as received (see
/// <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}.OnReceived"/>), whose origin says it came from the interface, and the
/// handler decides what to do with it, for example to send it on as if this user had originated it itself.
/// </summary>
/// <remarks>
/// The handler can also send a frame to every connected interface (see <see cref="INetworkContext{TFrame, TPriority, TLevel, TAspect}.SendInterface"/>). Interface connections are bidirectional MSMT session connections, so the frame
/// goes back down the connection the interface itself opened.
/// </remarks>
internal interface IInterfaceService : IAsyncDisposable
{
    /// <summary>Closes the listener and opens it again from the configuration as it is now (its port and certificates). Does nothing before <see cref="Start"/>.</summary>
    void Restart();

    /// <summary>Starts the inbound interface listener and blocks until <paramref name="cancellation"/> is cancelled.</summary>
    Task Start(CancellationToken cancellation);

    /// <summary>Sends <paramref name="frame"/> to every connected interface, completing once each has acknowledged it or failed. A connection that is lost while sending is skipped.</summary>
    /// <param name="priority">A configured priority.</param>
    /// <param name="frame">An instance of the configured frame type.</param>
    /// <exception cref="ArgumentException"><paramref name="priority"/> is not a configured priority.</exception>
    Task Send(Enum priority, object frame);
}

/// <inheritdoc cref="IInterfaceService" />
internal sealed class InterfaceService : IInterfaceService
{
    /// <summary>Initializes a new <see cref="InterfaceService"/>.</summary>
    public InterfaceService(
        IMsmtSessionPeer.IFactory peerFactory,
        IEngineController engineController,
        INetworkProcessing processing,
        IUserService userService,
        ILoggerFactory loggerFactory)
    {
        this.peerFactory = peerFactory;
        this.engineController = engineController;
        this.processing = processing;
        this.userService = userService;
        logger = loggerFactory.CreateLogger(LogCategories.App);
    }

    private readonly IMsmtSessionPeer.IFactory peerFactory;
    private readonly IEngineController engineController;
    private readonly INetworkProcessing processing;
    private readonly IUserService userService;
    private readonly ILogger logger;

    private readonly ConcurrentDictionary<IMsmtConnection, bool> connections = new();
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
                if (!restartRequested || cancellation.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    /// <inheritdoc />
    public void Restart()
    {
        lock (runLock)
        {
            if (current is null)
            {
                return;
            }

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
        listener.Connected.Listen(connection => connections[connection] = true);
        listener.Disconnected.Listen(disconnection => connections.TryRemove(disconnection.Connection, out _));
        listener.StartListener(engineController.InterfacePort, "127.0.0.1");

        await WaitForRestart(cancellation);
        await listener.DisposeAsync();
        connections.Clear();
        if (ReferenceEquals(peer, listener))
        {
            peer = null;
        }
    }

    public async Task Send(Enum priority, object frame)
    {
        using IMemoryOwner<byte> body = engineController.FrameSerializer.Serialize(frame);
        MsmtSendOptions options = new() { Priority = engineController.SendPriority(priority) };
        await Task.WhenAll(connections.Keys.Select(connection => Deliver(connection, body.Memory, options)));
    }

    private static async Task Deliver(IMsmtConnection connection, ReadOnlyMemory<byte> body, MsmtSendOptions options)
    {
        try
        {
            MsmtResponse response = await connection.Request(body, options);
            response.Payload?.Dispose();
        }
        catch (Exception ex) when (ex is ObjectDisposedException or TimeoutException or IOException)
        {
            // The interface went away while the frame was being sent; the others still get it.
        }
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

    internal Task HandleInterfaceMessage(ReadOnlyMemory<byte> data)
    {
        object message;
        try
        {
            message = engineController.FrameSerializer.Deserialize(data, null);
        }
        catch
        {
            return Task.CompletedTask;
        }

        // FrameSerializer determines the type from the data itself, so bytes from an incompatible sender
        // could describe a type other than this instance's own FrameType; treat that the same as a
        // failed deserialize rather than let a mismatched cast below throw.
        if (message.GetType() != engineController.FrameType)
        {
            message.TryDispose();
            return Task.CompletedTask;
        }

        UserInfo? userInfo = userService.GetCurrentUserInfo();
        if (userInfo is null)
        {
            message.TryDispose();
            return Task.CompletedTask;
        }

        processing.Received(message, FrameOrigin.Interface, userInfo.Name);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (peer is not null)
        {
            await peer.DisposeAsync();
        }
    }
}
