namespace BlueHeighliner.Comlink;

/// <summary>
/// Wraps another <see cref="IPeerTransport"/> and logs, byte for byte, everything it is asked to send and everything it receives, with the user on the other end of the connection.
/// It changes nothing else: what it is given travels on unchanged. Placed around the whole stack beneath the packetizer it traces packets, and around the packetizer it traces frames.
/// </summary>
internal sealed class TracingPeerTransport : IPeerTransport
{
    /// <summary>Initializes a new <see cref="TracingPeerTransport"/> over <paramref name="inner"/>.</summary>
    /// <param name="inner">The transport traced.</param>
    /// <param name="logger">The logger of the trace category.</param>
    /// <param name="settings">Says whether the trace category is on, asked for every entry so that turning it on takes effect on connections already made.</param>
    /// <param name="category">The trace category.</param>
    /// <param name="sent">The event written for bytes sent.</param>
    /// <param name="receivedEvent">The event written for bytes received.</param>
    public TracingPeerTransport(IPeerTransport inner, ILogger logger, ILogSettings settings, string category, EventId sent, EventId receivedEvent)
    {
        this.settings = settings;
        this.category = category;
        this.inner = inner;
        this.logger = logger;
        this.sent = sent;
        this.receivedEvent = receivedEvent;
        inner.Received.Listen(OnReceived);
        inner.Connected.Listen(connected.Publish);
        inner.Disconnected.Listen(disconnected.Publish);
    }

    private readonly IPeerTransport inner;
    private readonly ILogger logger;
    private readonly ILogSettings settings;
    private readonly string category;
    private readonly EventId sent;
    private readonly EventId receivedEvent;
    private readonly PeerEvent<PeerReceivedEventArgs> received = new();
    private readonly PeerEvent<PeerConnectionEventArgs> connected = new();
    private readonly PeerEvent<PeerConnectionEventArgs> disconnected = new();

    /// <inheritdoc />
    public IObservable<PeerReceivedEventArgs> Received => received;

    /// <inheritdoc />
    public IObservable<PeerConnectionEventArgs> Connected => connected;

    /// <inheritdoc />
    public IObservable<PeerConnectionEventArgs> Disconnected => disconnected;

    /// <inheritdoc />
    public void StartListener(int port) => inner.StartListener(port);

    /// <inheritdoc />
    public void StopListener() => inner.StopListener();

    /// <inheritdoc />
    public void SetClosed(ConnectionPoint point, bool closed) => inner.SetClosed(point, closed);

    /// <inheritdoc />
    public void Reset(ConnectionPoint point) => inner.Reset(point);

    /// <inheritdoc />
    public Task<PeerConnection> Connect(ConnectionPoint point, CancellationToken cancellation = default) => inner.Connect(point, cancellation);

    /// <inheritdoc />
    public Task<bool> Request(PeerConnection connection, ReadOnlyMemory<byte> data, PeerSendOptions? options = null, CancellationToken cancellation = default)
    {
        if (settings.IsEnabled(category)) { logger.Record(sent, "Sent {Length} bytes to {User}: {Bytes}", data.Length, UserOf(connection), Convert.ToHexString(data.Span)); }
        return inner.Request(connection, data, options, cancellation);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => inner.DisposeAsync();

    private static string UserOf(PeerConnection connection) => connection.User?.Name ?? connection.InitialUser ?? "unidentified";

    private void OnReceived(PeerReceivedEventArgs args)
    {
        if (settings.IsEnabled(category)) { logger.Record(receivedEvent, "Received {Length} bytes from {User}: {Bytes}", args.Payload.Length, UserOf(args.Connection), Convert.ToHexString(args.Payload.Span)); }
        received.Publish(args);
    }
}
