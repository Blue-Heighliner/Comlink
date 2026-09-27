namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>
/// One persistent point-to-point link to a remote node over a MicroGate serial port. A MicroGate peer is single
/// use, so this owns the loop that creates a new one, waits for the remote end to answer, and brings the link back up
/// whenever it is lost. Messages are fragmented into HDLC frames and answered with an accept or reject, giving the
/// same send-then-acknowledge contract as an IP connection.
/// </summary>
internal sealed class SerialLink : IAsyncDisposable
{
    private static int MaxMessageSize { get; } = 64 * 1024 * 1024;

    /// <summary>Starts connecting to <paramref name="endpoint"/> in the background and keeps trying until disposed.</summary>
    public SerialLink(
        UserEndpoint endpoint,
        IMicroGatePeerFactory peerFactory,
        ILogger logger,
        PeerEvent<PeerReceivedEventArgs> received,
        PeerEvent<PeerConnectionEventArgs> connected,
        PeerEvent<PeerConnectionEventArgs> disconnected,
        TimeSpan? reconnectDelay = null,
        TimeSpan? requestTimeout = null,
        bool startClosed = false)
    {
        this.endpoint = endpoint;
        this.peerFactory = peerFactory;
        this.logger = logger;
        this.received = received;
        this.connected = connected;
        this.disconnected = disconnected;
        this.reconnectDelay = reconnectDelay ?? TimeSpan.FromSeconds(2);
        this.requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(60);
        connection = new PeerConnection(endpoint, false, null, () => DropPeer(current));
        isClosed = startClosed;
        if (!startClosed) { openGate.TrySetResult(); }
        loop = Task.Run(Run);
    }

    private readonly UserEndpoint endpoint;
    private readonly IMicroGatePeerFactory peerFactory;
    private readonly ILogger logger;
    private readonly PeerEvent<PeerReceivedEventArgs> received;
    private readonly PeerEvent<PeerConnectionEventArgs> connected;
    private readonly PeerEvent<PeerConnectionEventArgs> disconnected;
    private readonly TimeSpan reconnectDelay;
    private readonly TimeSpan requestTimeout;
    private readonly PeerConnection connection;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task loop;
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<bool>> pending = new();
    private readonly Lock reassemblyLock = new();
    private readonly Lock closeLock = new();
    private TaskCompletionSource openGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private volatile IMicroGatePeer? current;
    private volatile bool isClosed;
    private CancellationTokenSource? attempt;
    private Reassembly? reassembly;
    private uint nextId;
    private bool failureLogged;
    private int disposed;

    /// <summary>Whether the link is currently up.</summary>
    public bool IsConnected => current is not null;

    /// <summary>
    /// Closes or reopens the link. While closed the device is released, no reconnection is attempted, and requests
    /// fail immediately; reopening starts connecting again.
    /// </summary>
    public void SetClosed(bool closed)
    {
        IMicroGatePeer? toDrop = null;
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

    /// <summary>Sends <paramref name="data"/> and waits for the remote node's accept or reject.</summary>
    /// <exception cref="IOException">The link is down, dropped while waiting, or the remote node did not answer in time.</exception>
    public async Task<bool> Request(ReadOnlyMemory<byte> data, PeerSendOptions? options, CancellationToken cancellation)
    {
        IMicroGatePeer peer = current ?? throw new IOException(isClosed ? $"Serial link to {endpoint} is closed" : $"Serial link to {endpoint} is not connected");
        uint id = Interlocked.Increment(ref nextId);
        TaskCompletionSource<bool> reply = new(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[id] = reply;
        try
        {
            await SendMessage(peer, id, data, cancellation);
            options?.Transmitted?.Invoke();
            return await reply.Task.WaitAsync(requestTimeout, cancellation);
        }
        catch (TimeoutException)
        {
            throw new IOException($"Serial link to {endpoint} did not acknowledge the message in time");
        }
        finally
        {
            pending.TryRemove(id, out _);
        }
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

    // Disposing a MicroGate peer sends a disconnect frame and can wait several seconds for it, so it never runs on
    // the caller's thread, which is the UI thread when a user closes or refreshes a connection.
    private static void DropPeer(IMicroGatePeer? peer)
    {
        if (peer is not null) { _ = Task.Run(() => DisposeQuietly(peer)); }
    }

    private static async Task DisposeQuietly(IMicroGatePeer peer)
    {
        try { await peer.DisposeAsync(); }
        catch { }
    }

    private async Task SendMessage(IMicroGatePeer peer, uint id, ReadOnlyMemory<byte> data, CancellationToken cancellation)
    {
        int chunkSize = peer.MaxPayloadSize - SerialFrame.DataHeaderSize;
        int count = Math.Max(1, (data.Length + chunkSize - 1) / chunkSize);
        if (count > ushort.MaxValue || data.Length > MaxMessageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(data), data.Length, "Message is too large to send over a serial link");
        }

        // Every fragment of one message is sent under the lock, so fragments of different messages never interleave
        // on the wire; the receiving side relies on this to keep only one reassembly in progress at a time.
        await sendLock.WaitAsync(cancellation);
        try
        {
            for (int index = 0; index < count; index++)
            {
                ReadOnlyMemory<byte> chunk = data.Slice(index * chunkSize, Math.Min(chunkSize, data.Length - (index * chunkSize)));
                await peer.Send(SerialFrame.EncodeData(id, (ushort)index, (ushort)count, chunk.Span), cancellation);
            }
        }
        finally
        {
            sendLock.Release();
        }
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
        IMicroGatePeer peer = peerFactory.Create();
        TaskCompletionSource ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
        peer.Received.Listen(OnFrame);
        peer.StateChanged.Listen(state => { if (state == MicroGatePeerState.Disconnected) { ended.TrySetResult(); } }, () => ended.TrySetResult());

        try
        {
            await peer.Start(endpoint.SerialPort!, new MicroGatePeerOptions { Address = endpoint.SerialAddress }, attemptToken);
        }
        catch (OperationCanceledException)
        {
            await DisposeQuietly(peer);
            return;
        }
        catch (Exception ex)
        {
            if (!failureLogged)
            {
                failureLogged = true;
                logger.LogWarning("Serial link to {Endpoint} cannot be established, retrying: {Message}", endpoint, ex.Message);
            }

            await DisposeQuietly(peer);
            await Delay();
            return;
        }

        failureLogged = false;
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

        logger.LogInformation("Serial link to {Endpoint} connected", endpoint);
        connected.Publish(new PeerConnectionEventArgs { Connection = connection });

        try { await ended.Task.WaitAsync(lifetime.Token); }
        catch (OperationCanceledException) { }

        lock (closeLock) { current = null; }
        FailPending();
        lock (reassemblyLock) { reassembly = null; }
        disconnected.Publish(new PeerConnectionEventArgs { Connection = connection });
        if (!lifetime.IsCancellationRequested && !isClosed) { logger.LogWarning("Serial link to {Endpoint} lost", endpoint); }

        await DisposeQuietly(peer);
        if (!isClosed) { await Delay(); }
    }

    private async Task Delay()
    {
        try { await Task.Delay(reconnectDelay, lifetime.Token); }
        catch (OperationCanceledException) { }
    }

    private void FailPending()
    {
        foreach (TaskCompletionSource<bool> reply in pending.Values)
        {
            reply.TrySetException(new IOException($"Serial link to {endpoint} was lost"));
        }
    }

    private void OnFrame(ReadOnlyMemory<byte> frame)
    {
        if (!SerialFrame.TryParse(frame, out SerialFrame parsed)) { return; }

        if (parsed.Kind == SerialFrameKind.Reply)
        {
            if (pending.TryGetValue(parsed.Id, out TaskCompletionSource<bool>? reply)) { reply.TrySetResult(parsed.Success); }
            return;
        }

        byte[]? message = Reassemble(parsed);
        if (message is not null)
        {
            uint id = parsed.Id;
            _ = Task.Run(() => Deliver(id, message));
        }
    }

    private byte[]? Reassemble(SerialFrame frame)
    {
        if (frame.Count == 1) { return frame.Chunk.ToArray(); }

        lock (reassemblyLock)
        {
            if (frame.Index == 0)
            {
                reassembly = new Reassembly(frame.Id, frame.Count);
            }
            else if (reassembly is null || reassembly.Id != frame.Id || reassembly.Next != frame.Index)
            {
                reassembly = null;
                return null;
            }

            reassembly.Append(frame.Chunk.Span);
            if (reassembly.Length > MaxMessageSize)
            {
                reassembly = null;
                return null;
            }

            if (reassembly.Next < reassembly.Count) { return null; }

            byte[] message = reassembly.ToArray();
            reassembly = null;
            return message;
        }
    }

    private async Task Deliver(uint id, byte[] message)
    {
        received.Publish(new PeerReceivedEventArgs { Connection = connection, Payload = message });

        IMicroGatePeer? peer = current;
        if (peer is null) { return; }

        try
        {
            await sendLock.WaitAsync(lifetime.Token);
            try { await peer.Send(SerialFrame.EncodeReply(id, true), lifetime.Token); }
            finally { sendLock.Release(); }
        }
        catch
        {
        }
    }

    private sealed class Reassembly(uint id, ushort count)
    {
        private readonly MemoryStream buffer = new();

        public uint Id { get; } = id;
        public ushort Count { get; } = count;
        public int Next { get; private set; }
        public long Length => buffer.Length;

        public void Append(ReadOnlySpan<byte> chunk)
        {
            buffer.Write(chunk);
            Next++;
        }

        public byte[] ToArray() => buffer.ToArray();
    }
}
