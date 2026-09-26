namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>
/// Presents the IP and serial transports as one: each request goes to whichever transport the endpoint belongs to,
/// and every transport's events are merged. The IP transport is <see langword="null"/> when it cannot be built
/// (no identity certificate), which leaves serial fully usable on a node that never uses IP.
/// </summary>
internal sealed class CompositePeerTransport : IPeerTransport
{
    /// <summary>Initializes a new <see cref="CompositePeerTransport"/>.</summary>
    public CompositePeerTransport(IPeerTransport? ip, IPeerTransport serial)
    {
        this.ip = ip;
        this.serial = serial;
        foreach (IPeerTransport transport in new[] { ip, serial }.OfType<IPeerTransport>())
        {
            transport.Received.Listen(received.Publish);
            transport.Connected.Listen(connected.Publish);
            transport.Disconnected.Listen(disconnected.Publish);
        }
    }

    private readonly IPeerTransport? ip;
    private readonly IPeerTransport serial;
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
    public void StartListener(int port) => ip?.StartListener(port);

    /// <inheritdoc />
    public void Open(UserEndpoint endpoint) => Select(endpoint)?.Open(endpoint);

    /// <inheritdoc />
    public void SetClosed(UserEndpoint endpoint, bool closed) => Select(endpoint)?.SetClosed(endpoint, closed);

    /// <inheritdoc />
    public void Reset(UserEndpoint endpoint) => Select(endpoint)?.Reset(endpoint);

    /// <inheritdoc />
    public Task<bool> Request(UserEndpoint target, ReadOnlyMemory<byte> data, PeerSendOptions? options = null, CancellationToken cancellation = default)
        => (Select(target) ?? throw new IOException($"IP connections are unavailable, cannot reach {target}")).Request(target, data, options, cancellation);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (ip is not null) { await ip.DisposeAsync(); }
        await serial.DisposeAsync();
    }

    private IPeerTransport? Select(UserEndpoint endpoint) => endpoint.IsSerial ? serial : ip;
}
