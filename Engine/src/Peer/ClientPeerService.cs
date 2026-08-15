namespace BlueHeighliner.Comlink.Engine.Peer;

/// <summary>
/// Implements <see cref="IPeerService"/> for <see cref="NodeRole.Client"/>: maintains a single
/// long-term outbound OFT connection to the configured server (<see cref="IEngineController"/>),
/// retrying indefinitely whenever the connection cannot be formed or drops. All outbound messages are
/// sent through this one connection regardless of addressee — the server performs the actual
/// user-to-connection routing. See <c>Docs/Peer.md</c>.
/// </summary>
internal sealed class ClientPeerService : IPeerService, IConnectionStatusService, IAsyncDisposable
{
    private static readonly TimeSpan defaultRetryInterval = TimeSpan.FromSeconds(5);

    /// <summary>Initializes a new <see cref="ClientPeerService"/>.</summary>
    public ClientPeerService(
        IOftConnector connector,
        IEngineController engineController,
        ILoggerFactory loggerFactory)
        : this(connector, engineController, loggerFactory, defaultRetryInterval)
    {
    }

    /// <summary>Initializes a new <see cref="ClientPeerService"/> with a custom retry interval; intended for unit testing.</summary>
    internal ClientPeerService(
        IOftConnector connector,
        IEngineController engineController,
        ILoggerFactory loggerFactory,
        TimeSpan retryInterval)
    {
        this.connector = connector;
        this.engineController = engineController;
        logger = loggerFactory.CreateLogger("ACTIVITY");
        this.retryInterval = retryInterval;
    }

    private readonly IOftConnector connector;
    private readonly IEngineController engineController;
    private readonly ILogger logger;
    private readonly TimeSpan retryInterval;

    private readonly ConcurrentDictionary<string, Task<bool>> inFlightSends = new();

    private volatile IOftConnection? activeConnection;
    private volatile string? remoteUserName;
    private DateTime? lastConnectedAt;
    private DateTime? lastDisconnectedAt;

    /// <inheritdoc />
    public event Func<object, Task>? MessageDelivered;

    /// <inheritdoc />
    public event Func<string, string, Task>? ConfirmationReceived;

#pragma warning disable CS0067 // No per-message OFT delivery status is tracked across the client/server hierarchy.
    /// <inheritdoc />
    public event Func<string, string, OftDeliveryStatus, Task>? DeliveryStatusChanged;

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

        while (!cancellation.IsCancellationRequested)
        {
            IOftConnection? connection = null;
            try
            {
                connection = await connector.Connect(endpoint.IpAddress, endpoint.Port, engineController.ConnectionOptions, cancellation);
                activeConnection = connection;
                remoteUserName = connection.Identity.Info;
                lastConnectedAt = DateTime.UtcNow;
                StatusesChanged?.Invoke();
                TaskCompletionSource disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ReceivedHandler = OnReceived;
                connection.DisconnectedHandler = _ => disconnected.TrySetResult();
                logger.LogInformation("Connected to server");
                await disconnected.Task.WaitAsync(cancellation);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Connection to server failed: {Message}", ex.Message);
            }
            finally
            {
                activeConnection = null;
                if (connection is not null)
                {
                    lastDisconnectedAt = DateTime.UtcNow;
                    StatusesChanged?.Invoke();
                    await connection.DisposeAsync();
                }
            }

            try { await Task.Delay(retryInterval, cancellation); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <inheritdoc />
    public Task<bool> Send(string userName, object message, CancellationToken cancellation = default)
    {
        // Route()/MessageRoutingService calls Send once per resolved recipient, even for a single group
        // address expanding to several users; since every send here goes through the one shared server
        // connection regardless of userName, in-flight sends are coalesced by message ID to avoid
        // transmitting the same message multiple times.
        string messageId = engineController.GetMessageId(message);
        return inFlightSends.GetOrAdd(messageId, _ => SendOnceAndCleanup(messageId, message, cancellation));
    }

    private async Task<bool> SendOnceAndCleanup(string messageId, object message, CancellationToken cancellation)
    {
        try { return await SendOnce(message, cancellation); }
        finally { inFlightSends.TryRemove(messageId, out _); }
    }

    /// <inheritdoc />
    public IReadOnlyList<PeerConnectionStatus> GetStatuses()
        => [new PeerConnectionStatus
        {
            UserName = remoteUserName ?? string.Empty,
            Kind = PeerConnectionKind.Server,
            IsConnected = activeConnection is not null,
            LastConnectedAt = lastConnectedAt,
            LastDisconnectedAt = lastDisconnectedAt
        }];

    private async Task<bool> SendOnce(object message, CancellationToken cancellation)
    {
        IOftConnection? connection = activeConnection;
        if (connection is null || !connection.IsConnected) { return false; }

        using OwnedBuffer buf = PeerSerializer.Serialize(message);
        try
        {
            await connection.Send(buf.Memory, priority: engineController.GetPriority(message), cancellationToken: cancellation);
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

    private void OnReceived(IMemoryOwner<byte> data)
    {
        byte[] copy;
        using (data) { copy = data.Memory.ToArray(); }
        _ = Task.Run(() => HandleMessage(copy));
    }

    internal Task<bool> HandleMessage(ReadOnlyMemory<byte> data)
        => PeerMessageDispatcher.Dispatch(data, engineController, logger, MessageDelivered, ConfirmationReceived);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        IOftConnection? connection = activeConnection;
        if (connection is not null) { await connection.DisposeAsync(); }
    }
}
