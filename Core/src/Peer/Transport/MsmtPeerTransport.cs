namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>
/// The IP half of the peer transport: adapts an <see cref="IMsmtPeer"/> (mutually authenticated TLS with
/// per-message acknowledgement) to <see cref="IPeerTransport"/>. IP endpoints are dialed on demand by MSMT and
/// kept as cached session connections.
/// </summary>
internal sealed class MsmtPeerTransport : IPeerTransport
{
    /// <summary>Initializes a new <see cref="MsmtPeerTransport"/> over <paramref name="peer"/>.</summary>
    public MsmtPeerTransport(IMsmtPeer peer)
    {
        this.peer = peer;
        peer.Connected.Listen(OnConnected);
        peer.Disconnected.Listen(OnDisconnected);
        peer.Received.Listen(OnReceived);
        peer.PackageChanged.Listen(OnPackageChanged);
    }

    private readonly IMsmtPeer peer;
    private readonly ConcurrentDictionary<IMsmtConnection, PeerConnection> connections = new();
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
    public void StartListener(int port) => peer.StartListener(port);

    /// <inheritdoc />
    public void Open(UserEndpoint endpoint)
    {
    }

    /// <inheritdoc />
    public async Task<bool> Request(UserEndpoint target, ReadOnlyMemory<byte> data, PeerSendOptions? options = null, CancellationToken cancellation = default)
    {
        MsmtSendOptions sendOptions = new() { Priority = options?.Priority ?? 0, Tag = options?.Transmitted is { } transmitted ? new TransmittedTag(transmitted) : null };
        MsmtResponse response = await peer.Request(new MsmtTarget { Host = target.IpAddress, Port = target.Port }, data, sendOptions, cancellation);
        response.Payload.Dispose();
        return response.Success;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => peer.DisposeAsync();

    private PeerConnection Wrap(IMsmtConnection connection)
        => connections.GetOrAdd(connection, static c => c.Sender is not null
            ? new PeerConnection(new UserEndpoint { IpAddress = c.Target.Host, Port = c.Target.Port }, false, c.Identity?.Subject, c.Drop)
            : new PeerConnection(null, true, c.Identity?.Subject, c.Drop));

    private void OnConnected(MsmtConnectedEventArgs args)
        => connected.Publish(new PeerConnectionEventArgs { Connection = Wrap(args.Connection) });

    private void OnDisconnected(MsmtDisconnectedEventArgs args)
    {
        if (connections.TryRemove(args.Connection, out PeerConnection? connection))
        {
            disconnected.Publish(new PeerConnectionEventArgs { Connection = connection });
        }
    }

    private void OnReceived(MsmtReceivedEventArgs args)
    {
        byte[] copy;
        using (args.Payload) { copy = args.Payload.Memory.ToArray(); }
        received.Publish(new PeerReceivedEventArgs { Connection = Wrap(args.Link.Connection), Payload = copy });
    }

    private static void OnPackageChanged(MsmtPackageChangedEventArgs args)
    {
        if (args.Package.Tag is TransmittedTag tag && args.Status == MsmtSendStatus.PendingAcknowledgement)
        {
            tag.Transmitted();
        }
    }

    private sealed record TransmittedTag(Action Transmitted);
}
