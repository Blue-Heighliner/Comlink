namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>
/// The IP half of the peer transport: adapts an <see cref="IMsmtSessionPeer"/> (mutually authenticated TLS
/// session connections, each with per-message acknowledgement) to <see cref="IPeerTransport"/>. An outbound
/// session connection is opened the first time an endpoint is sent to, and reused for every later request to
/// the same endpoint until it disconnects, matching MSMT's own idle keep-alive rather than reconnecting per send.
/// </summary>
internal sealed class MsmtPeerTransport : IPeerTransport
{
    /// <summary>Initializes a new <see cref="MsmtPeerTransport"/> over <paramref name="peer"/>.</summary>
    public MsmtPeerTransport(IMsmtSessionPeer peer)
    {
        this.peer = peer;
        peer.Connected.Listen(MarkConnected);
        peer.Disconnected.Listen(OnDisconnected);
        peer.PackageChanged.Listen(OnPackageChanged);
        peer.Receiver = OnReceived;
    }

    private readonly IMsmtSessionPeer peer;
    private readonly ConcurrentDictionary<IMsmtConnection, PeerConnection> connections = new();
    private readonly ConcurrentDictionary<string, IMsmtConnection> outbound = new();
    private readonly ConcurrentDictionary<string, bool> closed = new();
    private readonly Lock outboundLock = new();
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

        IMsmtConnection connection = await GetConnection(target, cancellation);
        MsmtSendOptions sendOptions = new() { Priority = options?.Priority ?? 0, Tag = options?.Transmitted is { } transmitted ? new TransmittedTag(transmitted) : null };
        try
        {
            MsmtResponse response = await connection.Request(data, sendOptions, cancellation);
            response.Payload?.Dispose();
            return response.Success;
        }
        catch (Exception ex) when (ex is ObjectDisposedException or TimeoutException)
        {
            throw new IOException($"Connection to {target} was lost", ex);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => peer.DisposeAsync();

    private static MsmtNameTarget ToMsmtTarget(UserEndpoint endpoint) => new() { Host = endpoint.IpAddress, Port = endpoint.Port, ServerName = endpoint.IpAddress };

    private async Task<IMsmtConnection> GetConnection(UserEndpoint target, CancellationToken cancellation)
    {
        bool created;
        IMsmtConnection connection;
        lock (outboundLock)
        {
            created = !outbound.TryGetValue(target.Key, out connection!) || connection.Status == MsmtConnectionStatus.Disconnected;
            if (created)
            {
                connection = peer.Connect(ToMsmtTarget(target));
                outbound[target.Key] = connection;
            }
        }

        if (!await connection.Wait(cancellation))
        {
            throw new IOException($"Could not connect to {target}");
        }

        if (created) { MarkConnected(connection); }
        return connection;
    }

    private PeerConnection Wrap(IMsmtConnection connection)
        => connections.GetOrAdd(connection, static c => c.Direction == MsmtConnectionDirection.Outgoing
            ? new PeerConnection(new UserEndpoint { IpAddress = c.Remote.Host, Port = c.Remote.Port }, false, c.Identity?.Subject, c.Dispose)
            : new PeerConnection(null, true, c.Identity?.Subject, c.Dispose));

    private void DropOutbound(UserEndpoint endpoint)
    {
        if (outbound.TryGetValue(endpoint.Key, out IMsmtConnection? connection)) { connection.Dispose(); }
    }

    private void MarkConnected(IMsmtConnection connection) => connected.Publish(new PeerConnectionEventArgs { Connection = Wrap(connection) });

    private void OnDisconnected(MsmtDisconnection args)
    {
        if (connections.TryRemove(args.Connection, out PeerConnection? connection))
        {
            if (connection.Endpoint is { } endpoint) { outbound.TryRemove(new KeyValuePair<string, IMsmtConnection>(endpoint.Key, args.Connection)); }
            disconnected.Publish(new PeerConnectionEventArgs { Connection = connection });
        }
    }

    private ValueTask<MsmtReceiveResult?> OnReceived(IMsmtConnection connection, ReadOnlyMemory<byte> payload, bool isResponseRequested)
    {
        received.Publish(new PeerReceivedEventArgs { Connection = Wrap(connection), Payload = payload.ToArray() });
        return ValueTask.FromResult<MsmtReceiveResult?>(isResponseRequested ? MsmtReceiveResult.Accept() : null);
    }

    private static void OnPackageChanged(MsmtPackageChange args)
    {
        if (args.Package.Tag is TransmittedTag tag && args.Status == MsmtSendStatus.PendingAcknowledgement)
        {
            tag.Transmitted();
        }
    }

    private sealed record TransmittedTag(Action Transmitted);
}
