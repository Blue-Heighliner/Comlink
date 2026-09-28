namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>
/// Presents the IP and serial transports as one: each connect goes to whichever transport the point belongs to, and each request to the one its connection runs over,
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
    public void SetClosed(ConnectionPoint point, bool closed) => Select(point)?.SetClosed(point, closed);

    /// <inheritdoc />
    public void Reset(ConnectionPoint point) => Select(point)?.Reset(point);

    /// <inheritdoc />
    public Task<PeerConnection> Connect(ConnectionPoint point, CancellationToken cancellation = default)
        => (Select(point) ?? throw new IOException($"IP connections are unavailable, cannot reach {point}")).Connect(point, cancellation);

    /// <inheritdoc />
    public Task<bool> Request(PeerConnection connection, ReadOnlyMemory<byte> data, PeerSendOptions? options = null, CancellationToken cancellation = default)
        => (connection.IsSerial ? serial : ip ?? throw new IOException("IP connections are unavailable")).Request(connection, data, options, cancellation);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (ip is not null) { await ip.DisposeAsync(); }
        await serial.DisposeAsync();
    }

    private IPeerTransport? Select(ConnectionPoint point) => point.IsSerial ? serial : ip;
}
