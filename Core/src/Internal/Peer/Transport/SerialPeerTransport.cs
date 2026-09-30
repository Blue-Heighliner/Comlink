namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>
/// The serial half of the peer transport: one persistent <see cref="SerialLink"/> per distinct serial point, created
/// the first time it is connected to. A serial cable joins exactly two nodes and is opened from both ends, so there is
/// no listener and no certificate exchange: who is on the other end is worked out from the port and address, or from the
/// connection message.
/// </summary>
internal sealed class SerialPeerTransport : IPeerTransport
{
    /// <summary>Initializes a new <see cref="SerialPeerTransport"/>.</summary>
    public SerialPeerTransport(IMicroGatePeerFactory peerFactory, ILogger logger, TimeSpan? reconnectDelay = null, TimeSpan? requestTimeout = null, MicroGatePeerOptions? options = null)
    {
        this.peerFactory = peerFactory;
        this.options = options ?? new();
        this.logger = logger;
        this.reconnectDelay = reconnectDelay;
        this.requestTimeout = requestTimeout;
    }

    private readonly IMicroGatePeerFactory peerFactory;
    private readonly MicroGatePeerOptions options;
    private readonly ILogger logger;
    private readonly TimeSpan? reconnectDelay;
    private readonly TimeSpan? requestTimeout;
    private readonly ConcurrentDictionary<string, Lazy<SerialLink>> links = new();
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
    public void StartListener(int port)
    {
    }

    /// <inheritdoc />
    public void StopListener()
    {
    }

    /// <inheritdoc />
    public void SetClosed(ConnectionPoint point, bool closed) => GetLink(point, closed).SetClosed(closed);

    /// <inheritdoc />
    public void Reset(ConnectionPoint point)
    {
        if (links.TryGetValue(point.Key, out Lazy<SerialLink>? link) && link.IsValueCreated) { link.Value.Reset(); }
    }

    /// <inheritdoc />
    public Task<PeerConnection> Connect(ConnectionPoint point, CancellationToken cancellation = default)
    {
        SerialLink link = GetLink(point);
        return link.IsConnected
            ? Task.FromResult(link.Connection)
            : Task.FromException<PeerConnection>(new IOException(link.IsClosed ? $"Serial link to {point} is closed" : $"Serial link to {point} is not connected"));
    }

    /// <inheritdoc />
    public Task<bool> Request(PeerConnection connection, ReadOnlyMemory<byte> data, PeerSendOptions? options = null, CancellationToken cancellation = default)
        => GetLink(connection.Point ?? throw new ArgumentException("A serial connection always has a point", nameof(connection))).Request(data, options, cancellation);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (Lazy<SerialLink> link in links.Values.Where(l => l.IsValueCreated))
        {
            await link.Value.DisposeAsync();
        }
    }

    private SerialLink GetLink(ConnectionPoint point, bool startClosed = false)
    {
        if (!point.IsSerial) { throw new ArgumentException("Point is not a serial point", nameof(point)); }

        return links.GetOrAdd(point.Key, _ => new Lazy<SerialLink>(() => new SerialLink(point, peerFactory, options, logger, received, connected, disconnected, reconnectDelay, requestTimeout, startClosed))).Value;
    }
}
