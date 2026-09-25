namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>
/// The serial half of the peer transport: one persistent <see cref="SerialLink"/> per distinct serial endpoint,
/// created the first time it is opened or sent to. A serial cable joins exactly two nodes, so the remote node's
/// identity is simply whichever user is configured for that port; there is no certificate exchange and no listener.
/// </summary>
internal sealed class SerialPeerTransport : IPeerTransport
{
    /// <summary>Initializes a new <see cref="SerialPeerTransport"/>.</summary>
    public SerialPeerTransport(IMicroGatePeerFactory peerFactory, ILogger logger, TimeSpan? reconnectDelay = null, TimeSpan? requestTimeout = null)
    {
        this.peerFactory = peerFactory;
        this.logger = logger;
        this.reconnectDelay = reconnectDelay;
        this.requestTimeout = requestTimeout;
    }

    private readonly IMicroGatePeerFactory peerFactory;
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
    public void Open(UserEndpoint endpoint) => GetLink(endpoint);

    /// <inheritdoc />
    public Task<bool> Request(UserEndpoint target, ReadOnlyMemory<byte> data, PeerSendOptions? options = null, CancellationToken cancellation = default)
        => GetLink(target).Request(data, options, cancellation);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (Lazy<SerialLink> link in links.Values.Where(l => l.IsValueCreated))
        {
            await link.Value.DisposeAsync();
        }
    }

    private SerialLink GetLink(UserEndpoint endpoint)
    {
        if (!endpoint.IsSerial) { throw new ArgumentException("Endpoint is not a serial endpoint", nameof(endpoint)); }

        return links.GetOrAdd(endpoint.Key, _ => new Lazy<SerialLink>(() => new SerialLink(endpoint, peerFactory, logger, received, connected, disconnected, reconnectDelay, requestTimeout))).Value;
    }
}
