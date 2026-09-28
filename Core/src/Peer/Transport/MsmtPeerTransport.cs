namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>
/// The IP half of the peer transport: adapts an <see cref="IMsmtSessionPeer"/> (mutually authenticated TLS
/// session connections, each with per-message acknowledgement) to <see cref="IPeerTransport"/>. An outbound
/// session connection is opened the first time a point is connected to, and reused until it disconnects, matching
/// MSMT's own idle keep-alive rather than reconnecting per send. Session connections are bidirectional, so a
/// connection the remote node opened carries this node's requests just like one this node opened.
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
    private readonly ConcurrentDictionary<PeerConnection, IMsmtConnection> handles = new();
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
    public void SetClosed(ConnectionPoint point, bool isClosed)
    {
        if (isClosed)
        {
            closed[point.Key] = true;
            DropOutbound(point);
        }
        else
        {
            closed.TryRemove(point.Key, out _);
        }
    }

    /// <inheritdoc />
    public void Reset(ConnectionPoint point)
    {
        if (!closed.ContainsKey(point.Key)) { DropOutbound(point); }
    }

    /// <inheritdoc />
    public async Task<PeerConnection> Connect(ConnectionPoint point, CancellationToken cancellation = default)
    {
        if (closed.ContainsKey(point.Key)) { throw new IOException($"Connection to {point} is closed"); }

        return Wrap(await GetConnection(point, cancellation));
    }

    /// <inheritdoc />
    public async Task<bool> Request(PeerConnection connection, ReadOnlyMemory<byte> data, PeerSendOptions? options = null, CancellationToken cancellation = default)
    {
        if (connection.Point is { } point && closed.ContainsKey(point.Key)) { throw new IOException($"Connection to {point} is closed"); }
        if (!handles.TryGetValue(connection, out IMsmtConnection? handle) || handle.Status == MsmtConnectionStatus.Disconnected) { throw new IOException("The connection is no longer open"); }

        // MSMT orders sends by the negated priority, which overflows for int.MinValue and would put the lowest priority first.
        MsmtSendOptions sendOptions = new() { Priority = Math.Max(options?.Priority ?? 0, int.MinValue + 1), Tag = options?.Transmitted is { } transmitted ? new TransmittedTag(transmitted) : null };
        try
        {
            MsmtResponse response = await handle.Request(data, sendOptions, cancellation);
            response.Payload?.Dispose();
            return response.Success;
        }
        catch (Exception ex) when (ex is ObjectDisposedException or TimeoutException)
        {
            throw new IOException("The connection was lost", ex);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => peer.DisposeAsync();

    private static MsmtNameTarget ToMsmtTarget(ConnectionPoint point) => new() { Host = point.IpAddress, Port = point.Port, ServerName = point.IpAddress };

    private async Task<IMsmtConnection> GetConnection(ConnectionPoint target, CancellationToken cancellation)
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
    {
        if (connections.TryGetValue(connection, out PeerConnection? existing)) { return existing; }

        string? subject = connection.Identity?.Subject;
        ConnectionInfo info = new()
        {
            IsInbound = connection.Direction != MsmtConnectionDirection.Outgoing,
            Host = connection.Remote.Host,
            Port = connection.Remote.Port,
            CertificateSubject = subject,
            CertificateNames = subject is null ? [] : PeerIdentity.ExtractCommonNames(subject)
        };
        PeerConnection created = new(info.IsInbound ? null : new ConnectionPoint { IpAddress = connection.Remote.Host, Port = connection.Remote.Port }, info, connection.Dispose);
        handles[created] = connection;
        PeerConnection winner = connections.GetOrAdd(connection, created);
        if (!ReferenceEquals(winner, created)) { handles.TryRemove(created, out _); }
        return winner;
    }

    // Removed as well as disposed: disposing only starts the close, so the connection can still report itself connected for
    // a moment, and the next request must not reuse it.
    private void DropOutbound(ConnectionPoint point)
    {
        if (outbound.TryRemove(point.Key, out IMsmtConnection? connection)) { connection.Dispose(); }
    }

    private void MarkConnected(IMsmtConnection connection) => connected.Publish(new PeerConnectionEventArgs { Connection = Wrap(connection) });

    private void OnDisconnected(MsmtDisconnection args)
    {
        if (connections.TryRemove(args.Connection, out PeerConnection? connection))
        {
            handles.TryRemove(connection, out _);
            if (connection.Point is { } point) { outbound.TryRemove(new KeyValuePair<string, IMsmtConnection>(point.Key, args.Connection)); }
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

    // MSMT tracks tagged sends by tag, replacing an earlier send that has an equal one, so tags must be equal only to
    // themselves: sends that share a callback (the packets of one payload) would otherwise overwrite each other.
    private sealed class TransmittedTag(Action transmitted)
    {
        public void Transmitted() => transmitted();
    }
}
