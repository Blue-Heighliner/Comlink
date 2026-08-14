namespace BlueHeighliner.Comlink.Engine.Peer;

/// <summary>
/// Implements <see cref="IPeerService"/> for <see cref="NodeRole.Client"/>: maintains a single
/// long-term outbound OFT connection to the configured server (<see cref="INetworkTopology"/>),
/// retrying indefinitely whenever the connection cannot be formed or drops. All outbound messages are
/// sent through this one connection regardless of addressee — the server performs the actual
/// user-to-connection routing. See <c>Docs/Peer.md</c>.
/// </summary>
internal sealed class ClientPeerService : IPeerService, IAsyncDisposable
{
    private static readonly TimeSpan DefaultRetryInterval = TimeSpan.FromSeconds(5);

    private readonly IOftConnector _connector;
    private readonly INetworkTopology _networkTopology;
    private readonly IOftCertificateProvider _certProvider;
    private readonly IMessageFormat _messageFormat;
    private readonly ILogger _logger;
    private readonly TimeSpan _retryInterval;
    private readonly ConcurrentDictionary<string, Task<bool>> _inFlightSends = new();

    private volatile IOftConnection? _connection;

    /// <inheritdoc />
    public event Func<object, Task>? MessageDelivered;
    /// <inheritdoc />
    public event Func<string, string, Task>? ConfirmationReceived;
#pragma warning disable CS0067 // No per-message OFT delivery status is tracked across the client/server hierarchy.
    /// <inheritdoc />
    public event Func<string, string, OftDeliveryStatus, Task>? DeliveryStatusChanged;
#pragma warning restore CS0067

    /// <summary>Initializes a new <see cref="ClientPeerService"/>.</summary>
    public ClientPeerService(
        IOftConnector connector,
        INetworkTopology networkTopology,
        IOftCertificateProvider certProvider,
        IMessageFormat messageFormat,
        ILoggerFactory loggerFactory)
        : this(connector, networkTopology, certProvider, messageFormat, loggerFactory, DefaultRetryInterval)
    {
    }

    /// <summary>Initializes a new <see cref="ClientPeerService"/> with a custom retry interval; intended for unit testing.</summary>
    internal ClientPeerService(
        IOftConnector connector,
        INetworkTopology networkTopology,
        IOftCertificateProvider certProvider,
        IMessageFormat messageFormat,
        ILoggerFactory loggerFactory,
        TimeSpan retryInterval)
    {
        _connector = connector;
        _networkTopology = networkTopology;
        _certProvider = certProvider;
        _messageFormat = messageFormat;
        _logger = loggerFactory.CreateLogger("ACTIVITY");
        _retryInterval = retryInterval;
    }

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        UserEndpoint? endpoint = _networkTopology.GetServerEndpoint();
        if (endpoint is null)
        {
            _logger.LogError("Client role requires a configured server endpoint; none was provided");
            return;
        }

        while (!cancellation.IsCancellationRequested)
        {
            IOftConnection? connection = null;
            try
            {
                connection = await _connector.Connect(endpoint.IpAddress, endpoint.Port, _certProvider.GetPeerOptions(), cancellation);
                _connection = connection;
                TaskCompletionSource disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ReceivedHandler = OnReceived;
                connection.DisconnectedHandler = _ => disconnected.TrySetResult();
                _logger.LogInformation("Connected to server");
                await disconnected.Task.WaitAsync(cancellation);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Connection to server failed: {Message}", ex.Message);
            }
            finally
            {
                _connection = null;
                if (connection is not null) await connection.DisposeAsync();
            }

            try { await Task.Delay(_retryInterval, cancellation); }
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
        string messageId = _messageFormat.GetMessageId(message);
        return _inFlightSends.GetOrAdd(messageId, _ => SendOnceAndCleanup(messageId, message, cancellation));
    }

    private async Task<bool> SendOnceAndCleanup(string messageId, object message, CancellationToken cancellation)
    {
        try { return await SendOnce(message, cancellation); }
        finally { _inFlightSends.TryRemove(messageId, out _); }
    }

    private async Task<bool> SendOnce(object message, CancellationToken cancellation)
    {
        IOftConnection? connection = _connection;
        if (connection is null || !connection.IsConnected) return false;

        using OwnedBuffer buf = PeerSerializer.Serialize(message);
        try
        {
            await connection.Send(buf.Memory, priority: _messageFormat.GetPriority(message), cancellationToken: cancellation);
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
        _logger.LogInformation("{MessageId} delivered locally from {FromUser}", _messageFormat.GetMessageId(payload), _messageFormat.GetFromUser(payload));
        if (MessageDelivered is not null)
            await MessageDelivered(payload);
    }

    private void OnReceived(IMemoryOwner<byte> data)
    {
        byte[] copy;
        using (data) { copy = data.Memory.ToArray(); }
        _ = Task.Run(() => HandleMessage(copy));
    }

    internal Task<bool> HandleMessage(ReadOnlyMemory<byte> data) =>
        PeerMessageDispatcher.Dispatch(data, _messageFormat, _logger, MessageDelivered, ConfirmationReceived);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        IOftConnection? connection = _connection;
        if (connection is not null) await connection.DisposeAsync();
    }
}
