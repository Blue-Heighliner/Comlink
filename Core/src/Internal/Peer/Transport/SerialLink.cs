namespace BlueHeighliner.Comlink;

/// <summary>
/// One persistent point-to-point link to a remote node over a MicroGate serial port. A HDLC peer is single
/// use, so this owns the loop that creates a new one, waits for the remote end to answer, and brings the link back up
/// whenever it is lost. Nothing but serialized messages or packets crosses the cable: each payload is exactly one HDLC
/// information frame, carrying no header, fragment number or reply of this software's own, so a system that does not run it can take part.
/// </summary>
internal sealed class SerialLink : IAsyncDisposable
{
    /// <summary>Starts connecting to <paramref name="point"/> in the background and keeps trying until disposed.</summary>
    public SerialLink(
        ConnectionPoint point,
        IHdlcPeerFactory peerFactory,
        HdlcPeerOptions options,
        ILogger logger,
        PeerEvent<PeerReceivedEventArgs> received,
        PeerEvent<PeerConnectionEventArgs> connected,
        PeerEvent<PeerConnectionEventArgs> disconnected,
        TimeSpan? reconnectDelay = null,
        bool startClosed = false,
        TimeSpan? candidateTimeout = null)
    {
        this.point = point;
        this.peerFactory = peerFactory;
        this.options = options;
        this.logger = logger;
        this.received = received;
        this.connected = connected;
        this.disconnected = disconnected;
        this.reconnectDelay = reconnectDelay ?? TimeSpan.FromSeconds(2);
        this.candidateTimeout = candidateTimeout ?? TimeSpan.FromSeconds(4);
        remotes = [point.RemoteSerialAddress, .. point.OtherRemotes.Select(remote => remote.Address)];
        connection = new PeerConnection(point, new SerialConnectionInfo { SerialPort = point.SerialPort!, SerialAddress = point.SerialAddress, RemoteSerialAddress = point.RemoteSerialAddress }, () => DropPeer(current));
        isClosed = startClosed;
        if (!startClosed) { openGate.TrySetResult(); }
        loop = Task.Run(Run);
    }

    private readonly ConnectionPoint point;
    private readonly IHdlcPeerFactory peerFactory;
    private readonly HdlcPeerOptions options;
    private readonly ILogger logger;
    private readonly PeerEvent<PeerReceivedEventArgs> received;
    private readonly PeerEvent<PeerConnectionEventArgs> connected;
    private readonly PeerEvent<PeerConnectionEventArgs> disconnected;
    private readonly TimeSpan reconnectDelay;
    private readonly TimeSpan candidateTimeout;
    private readonly byte[] remotes;
    private int nextRemote;
    private readonly PeerConnection connection;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task loop;
    private readonly PriorityLock sendLock = new();
    private readonly Lock closeLock = new();
    private TaskCompletionSource openGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private volatile IHdlcPeer? current;
    private volatile bool isClosed;
    private CancellationTokenSource? attempt;
    private bool failureLogged;
    private int disposed;

    /// <summary>Whether the link is currently up.</summary>
    public bool IsConnected => current is not null;

    /// <summary>Whether the link has been closed and is not trying to reconnect.</summary>
    public bool IsClosed => isClosed;

    /// <summary>The connection this link presents to the peer services, the same object across reconnects.</summary>
    public PeerConnection Connection => connection;

    /// <summary>
    /// Closes or reopens the link. While closed the device is released, no reconnection is attempted, and requests
    /// fail immediately; reopening starts connecting again.
    /// </summary>
    public void SetClosed(bool closed)
    {
        IHdlcPeer? toDrop = null;
        lock (closeLock)
        {
            isClosed = closed;
            if (closed)
            {
                if (openGate.Task.IsCompleted) { openGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); }
                attempt?.Cancel();
                toDrop = current;
            }
            else
            {
                openGate.TrySetResult();
            }
        }

        DropPeer(toDrop);
    }

    /// <summary>Drops the current link, if there is one; the connect loop then brings up a new one.</summary>
    public void Reset()
    {
        if (!isClosed) { DropPeer(current); }
    }

    /// <summary>Sends <paramref name="data"/> as one HDLC information frame, once the frame is on the link.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="data"/> does not fit one frame; enable packetization with a packet size that does.</exception>
    /// <exception cref="IOException">The link is down or was lost while sending.</exception>
    public async Task<bool> Request(ReadOnlyMemory<byte> data, PeerSendOptions? options, CancellationToken cancellation)
    {
        IHdlcPeer peer = current ?? throw new IOException(isClosed ? $"Serial link to {point} is closed" : $"Serial link to {point} is not connected");
        if (data.Length > peer.MaxPayloadSize)
        {
            logger.LogError("A payload of {Length} bytes cannot be sent over {Point}: an HDLC frame carries at most {Max} bytes (MaxInfoField), and each frame or packet is sent as exactly one, so lower the packet size or raise MaxInfoField", data.Length, point, peer.MaxPayloadSize);
            throw new ArgumentOutOfRangeException(nameof(data), data.Length, $"The payload does not fit one HDLC frame of {peer.MaxPayloadSize} bytes");
        }

        try
        {
            using (await sendLock.Acquire(options?.Priority ?? 0, cancellation)) { await peer.Send(data, cancellation); }
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not IOException)
        {
            throw new IOException($"Serial link to {point} was lost", ex);
        }

        options?.Transmitted?.Invoke();
        return true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) { return; }

        await lifetime.CancelAsync();
        try { await loop; }
        catch (OperationCanceledException) { }
        lifetime.Dispose();
    }

    // Disposing a HDLC peer sends a disconnect frame and can wait several seconds for it, so it never runs on
    // the caller's thread, which is the UI thread when a user closes or refreshes a connection.
    private static void DropPeer(IHdlcPeer? peer)
    {
        if (peer is not null) { _ = Task.Run(() => DisposeQuietly(peer)); }
    }

    private static async Task DisposeQuietly(IHdlcPeer peer)
    {
        try { await peer.DisposeAsync(); }
        catch { }
    }

    private async Task Run()
    {
        while (!lifetime.IsCancellationRequested)
        {
            TaskCompletionSource gate;
            lock (closeLock) { gate = openGate; }
            try { await gate.Task.WaitAsync(lifetime.Token); }
            catch (OperationCanceledException) { return; }

            using CancellationTokenSource attemptSource = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            lock (closeLock)
            {
                attempt = attemptSource;
                if (isClosed) { attemptSource.Cancel(); }
            }

            try { await RunOnce(attemptSource.Token); }
            finally
            {
                lock (closeLock) { attempt = null; }
            }
        }
    }

    private async Task RunOnce(CancellationToken attemptToken)
    {
        IHdlcPeer peer = peerFactory.Create();
        TaskCompletionSource ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
        peer.Receiver = OnFrame;
        peer.Exceptions.Listen(ex => logger.LogWarning("Serial link to {Point} met an error: {Message}", point, ex.Message));
        peer.StateChanged.Listen(state => { if (state == HdlcPeerState.Disconnected) { ended.TrySetResult(); } }, () => ended.TrySetResult());

        // With several users that may be at the other end of the cable, each is tried in turn for a while, since the far end answers only the address it is given.
        byte remote = remotes[nextRemote % remotes.Length];
        using CancellationTokenSource candidate = CancellationTokenSource.CreateLinkedTokenSource(attemptToken);
        if (remotes.Length > 1) { candidate.CancelAfter(candidateTimeout); }

        try
        {
            await peer.Start(point.SerialPort!, options, attemptToken);
            await peer.Connect(point.SerialAddress, remote, candidate.Token);
        }
        catch (OperationCanceledException)
        {
            await DisposeQuietly(peer);
            if (!attemptToken.IsCancellationRequested) { nextRemote++; }
            return;
        }
        catch (Exception ex)
        {
            if (!failureLogged)
            {
                failureLogged = true;
                logger.LogWarning("Serial link to {Point} cannot be established, retrying: {Message}", point, ex.Message);
            }

            await DisposeQuietly(peer);
            await Delay();
            return;
        }

        failureLogged = false;
        ((SerialConnectionInfo)connection.Info).RemoteSerialAddress = remote;
        bool closedMeanwhile;
        lock (closeLock)
        {
            closedMeanwhile = isClosed;
            if (!closedMeanwhile) { current = peer; }
        }

        if (closedMeanwhile)
        {
            await DisposeQuietly(peer);
            return;
        }

        logger.LogInformation("Serial link to {Point} connected", point);
        connected.Publish(new PeerConnectionEventArgs { Connection = connection });

        try { await ended.Task.WaitAsync(lifetime.Token); }
        catch (OperationCanceledException) { }

        lock (closeLock) { current = null; }
        disconnected.Publish(new PeerConnectionEventArgs { Connection = connection });
        if (!lifetime.IsCancellationRequested && !isClosed) { logger.LogWarning("Serial link to {Point} lost", point); }

        await DisposeQuietly(peer);
        if (!isClosed) { await Delay(); }
    }

    private async Task Delay()
    {
        try { await Task.Delay(reconnectDelay, lifetime.Token); }
        catch (OperationCanceledException) { }
    }

    private void OnFrame(IMemoryOwner<byte> owner)
    {
        using IMemoryOwner<byte> frame = owner;
        received.Publish(new PeerReceivedEventArgs { Connection = connection, Payload = frame.Memory.ToArray() });
    }
}
