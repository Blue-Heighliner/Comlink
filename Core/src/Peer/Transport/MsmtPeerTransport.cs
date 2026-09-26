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
    private readonly ConcurrentDictionary<string, PeerConnection> outbound = new();
    private readonly ConcurrentDictionary<string, bool> closed = new();
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
    public void SetClosed(UserEndpoint endpoint, bool isClosed)
    {
        if (isClosed)
        {
            closed[endpoint.Key] = true;
            DropOutbound(endpoint);
        }
        else
        {
            closed.TryRemove(endpoint.Key, out _);
        }
    }

    /// <inheritdoc />
    public void Reset(UserEndpoint endpoint)
    {
        if (!closed.ContainsKey(endpoint.Key)) { DropOutbound(endpoint); }
    }

    /// <inheritdoc />
    public async Task<bool> Request(UserEndpoint target, ReadOnlyMemory<byte> data, PeerSendOptions? options = null, CancellationToken cancellation = default)
    {
        if (closed.ContainsKey(target.Key)) { throw new IOException($"Connection to {target} is closed"); }

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

    private void DropOutbound(UserEndpoint endpoint)
    {
        if (outbound.TryGetValue(endpoint.Key, out PeerConnection? connection)) { connection.Drop(); }
    }

    private void OnConnected(MsmtConnectedEventArgs args)
    {
        PeerConnection connection = Wrap(args.Connection);
        if (connection.Endpoint is { } endpoint) { outbound[endpoint.Key] = connection; }
        connected.Publish(new PeerConnectionEventArgs { Connection = connection });
    }

    private void OnDisconnected(MsmtDisconnectedEventArgs args)
    {
        if (connections.TryRemove(args.Connection, out PeerConnection? connection))
        {
            if (connection.Endpoint is { } endpoint) { outbound.TryRemove(new KeyValuePair<string, PeerConnection>(endpoint.Key, connection)); }
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
