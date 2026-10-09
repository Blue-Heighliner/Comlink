namespace BlueHeighliner.Comlink.Tests;

/// <summary>
/// One end of an in-memory pair of <see cref="IPeerTransport"/>s. Connecting on one end forms a connection on both, as an
/// outbound (or, for serial, symmetric) one here and an inbound (or serial) one on the other end, and a request on one
/// end is delivered to the other end's <see cref="Received"/> and acknowledged once its subscribers have run.
/// </summary>
internal sealed class LoopbackPeerTransport : IPeerTransport
{
    private readonly ConcurrentDictionary<PeerConnection, PeerConnection> peers = new();
    private readonly ConcurrentDictionary<string, PeerConnection> outbound = new();
    private readonly PeerEvent<PeerReceivedEventArgs> received = new();
    private readonly PeerEvent<PeerConnectionEventArgs> connected = new();
    private readonly PeerEvent<PeerConnectionEventArgs> disconnected = new();
    private LoopbackPeerTransport? remote;

    /// <summary>The certificate common names this end presents to the other end on an IP connection.</summary>
    public IReadOnlyList<string> CertificateNames { get; init; } = [];

    /// <summary>Gets or sets a value indicating whether requests on this end are acknowledged without ever being delivered to the other end.</summary>
    public bool IsSilent { get; set; }

    /// <summary>Gets a value indicating whether this end's connections are serial rather than IP.</summary>
    public bool IsSerial { get; init; }

    /// <summary>Gets or sets how long a request on this end is held before it is delivered, given the payload; a held request can be cancelled.</summary>
    public Func<byte[], TimeSpan>? DelayFor { get; set; }

    /// <summary>Every payload delivered to this end, in order.</summary>
    public ConcurrentQueue<byte[]> Delivered { get; } = [];

    /// <summary>Gets the options of every request this end made, in order.</summary>
    public ConcurrentQueue<PeerSendOptions?> Requests { get; } = [];

    /// <inheritdoc />
    public IObservable<PeerReceivedEventArgs> Received => received;

    /// <inheritdoc />
    public IObservable<PeerConnectionEventArgs> Connected => connected;

    /// <inheritdoc />
    public IObservable<PeerConnectionEventArgs> Disconnected => disconnected;

    /// <summary>Creates two ends that talk to each other.</summary>
    public static (LoopbackPeerTransport A, LoopbackPeerTransport B) CreatePair(IReadOnlyList<string>? aCertificateNames = null, IReadOnlyList<string>? bCertificateNames = null, bool serial = false)
    {
        LoopbackPeerTransport a = new() { CertificateNames = aCertificateNames ?? [], IsSerial = serial };
        LoopbackPeerTransport b = new() { CertificateNames = bCertificateNames ?? [], IsSerial = serial };
        a.remote = b;
        b.remote = a;
        return (a, b);
    }

    /// <inheritdoc />
    public void StartListener(int port)
    {
    }

    /// <inheritdoc />
    public void StopListener()
    {
    }

    /// <inheritdoc />
    public void SetClosed(ConnectionPoint point, bool closed)
    {
        if (closed && outbound.TryRemove(point.Key, out PeerConnection? connection))
        {
            connection.Drop();
        }
    }

    /// <inheritdoc />
    public void Reset(ConnectionPoint point)
    {
        if (outbound.TryRemove(point.Key, out PeerConnection? connection))
        {
            connection.Drop();
        }
    }

    /// <inheritdoc />
    public Task<PeerConnection> Connect(ConnectionPoint point, CancellationToken cancellation = default)
    {
        if (outbound.TryGetValue(point.Key, out PeerConnection? existing))
        {
            return Task.FromResult(existing);
        }

        LoopbackPeerTransport far = remote!;
        PeerConnection? local = null;
        PeerConnection? counterpart = null;
        local = new PeerConnection(
            point,
            IsSerial
                ? new SerialConnectionInfo { SerialPort = point.SerialPort!, SerialAddress = point.SerialAddress, RemoteSerialAddress = point.RemoteSerialAddress }
                : new IpConnectionInfo { Host = point.IpAddress, Port = point.Port, CertificateSubject = far.CertificateNames.Count > 0 ? $"CN={far.CertificateNames[0]}" : null, CertificateNames = far.CertificateNames },
            () => Break(local!, counterpart!));
        counterpart = new PeerConnection(
            IsSerial ? point : null,
            IsSerial
                ? new SerialConnectionInfo { SerialPort = point.SerialPort!, SerialAddress = point.RemoteSerialAddress, RemoteSerialAddress = point.SerialAddress }
                : new IpConnectionInfo { IsInbound = true, Host = "127.0.0.1", Port = 40000, CertificateSubject = CertificateNames.Count > 0 ? $"CN={CertificateNames[0]}" : null, CertificateNames = CertificateNames },
            () => Break(local!, counterpart!));
        peers[local] = counterpart;
        far.peers[counterpart] = local;
        outbound[point.Key] = local;
        connected.Publish(new PeerConnectionEventArgs { Connection = local });
        far.connected.Publish(new PeerConnectionEventArgs { Connection = counterpart });
        return Task.FromResult(local);
    }

    /// <inheritdoc />
    public async Task<bool> Request(PeerConnection connection, ReadOnlyMemory<byte> data, PeerSendOptions? options = null, CancellationToken cancellation = default)
    {
        if (!peers.TryGetValue(connection, out PeerConnection? counterpart))
        {
            throw new IOException("The connection is gone");
        }

        Requests.Enqueue(options);
        options?.Transmitted?.Invoke();
        if (IsSilent)
        {
            return true;
        }

        byte[] copy = data.ToArray();
        if (DelayFor?.Invoke(copy) is { } delay && delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellation);
        }
        await Task.Run(() =>
        {
            remote!.Delivered.Enqueue(copy);
            remote.received.Publish(new PeerReceivedEventArgs { Connection = counterpart, Payload = copy });
        }, cancellation);
        return true;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void Break(PeerConnection local, PeerConnection counterpart)
    {
        if (!peers.TryRemove(local, out _))
        {
            return;
        }

        LoopbackPeerTransport far = remote!;
        far.peers.TryRemove(counterpart, out _);
        if (local.Point is { } point)
        {
            outbound.TryRemove(new KeyValuePair<string, PeerConnection>(point.Key, local));
        }
        disconnected.Publish(new PeerConnectionEventArgs { Connection = local });
        far.disconnected.Publish(new PeerConnectionEventArgs { Connection = counterpart });
    }
}
