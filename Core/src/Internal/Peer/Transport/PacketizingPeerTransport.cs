namespace BlueHeighliner.Comlink;

/// <summary>
/// Wraps another <see cref="IPeerTransport"/> so that payloads travel as the prioritized packets an <see cref="IPacketizer"/>
/// breaks them into rather than as one message: a request sends every packet as its own request on the wrapped
/// transport, through a <see cref="PacketScheduler"/> per connection that always sends the highest-priority queued
/// packet next and never has more than a window of them in flight, so a higher-priority payload overtakes the remaining packets of a lower-priority one that is still
/// being transmitted; received packets are reassembled per connection and only a complete payload is published.
/// </summary>
internal sealed class PacketizingPeerTransport : IPeerTransport
{
    /// <summary>Initializes a new <see cref="PacketizingPeerTransport"/> over <paramref name="inner"/>.</summary>
    public PacketizingPeerTransport(IPeerTransport inner, IPacketizer packetizer, int window, ILogger logger)
    {
        this.inner = inner;
        this.packetizer = packetizer;
        this.window = window;
        this.logger = logger;
        inner.Received.Listen(OnReceived);
        inner.Connected.Listen(connected.Publish);
        inner.Disconnected.Listen(OnDisconnected);
    }

    private readonly IPeerTransport inner;
    private readonly IPacketizer packetizer;
    private readonly int window;
    private readonly ILogger logger;
    private readonly ConcurrentDictionary<PeerConnection, IPacketAssembler> assemblers = new();
    private readonly ConcurrentDictionary<PeerConnection, PacketScheduler> schedulers = new();
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
    public void StartListener(int port) => inner.StartListener(port);

    /// <inheritdoc />
    public void StopListener() => inner.StopListener();

    /// <inheritdoc />
    public void SetClosed(ConnectionPoint point, bool closed) => inner.SetClosed(point, closed);

    /// <inheritdoc />
    public void Reset(ConnectionPoint point) => inner.Reset(point);

    /// <inheritdoc />
    public Task<PeerConnection> Connect(ConnectionPoint point, CancellationToken cancellation = default) => inner.Connect(point, cancellation);

    /// <inheritdoc />
    public async Task<bool> Request(PeerConnection connection, ReadOnlyMemory<byte> data, PeerSendOptions? options = null, CancellationToken cancellation = default)
    {
        if (options?.IsPacket == true)
        {
            return await inner.Request(connection, data, options, cancellation);
        }

        IReadOnlyList<Packet> packets;
        try { packets = packetizer.Split(data, options?.Priority ?? 0, options?.Frame); }
        catch (ArgumentOutOfRangeException ex)
        {
            logger.Record(LogEvents.PayloadTooLarge, ex, "A payload of {Length} bytes cannot be sent over {Point}: {Reason}", data.Length, connection.Point, "it is too large to be split into packets, so nothing is sent");
            return false;
        }

        try
        {
            int untransmitted = packets.Count;
            Action? transmitted = null;
            if (options?.Transmitted is { } onTransmitted)
            {
                transmitted = () =>
                {
                    if (Interlocked.Decrement(ref untransmitted) == 0)
                    {
                        onTransmitted();
                    }
                };
            }

            // Every packet is queued before any is awaited, so the scheduler orders them against the packets of
            // other payloads. The packets of a payload that has failed are dropped rather than sent; none already
            // being sent is cancelled, since cancelling a packet already written closes the connection they share.
            PacketScheduler scheduler = schedulers.GetOrAdd(connection, key => new PacketScheduler(inner, key, window));
            PacketScheduler.Payload payload = new();
            Task<bool>[] sends = [.. packets.Select(packet => scheduler.Enqueue(packet, transmitted, payload, cancellation))];
            bool[] accepted = await Task.WhenAll(sends);
            return accepted.All(result => result);
        }
        finally
        {
            foreach (Packet packet in packets)
            {
                packet.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (IPacketAssembler assembler in assemblers.Values)
        {
            assembler.Dispose();
        }
        assemblers.Clear();
        foreach (PacketScheduler scheduler in schedulers.Values)
        {
            scheduler.Dispose();
        }
        await inner.DisposeAsync();
    }

    private void OnReceived(PeerReceivedEventArgs args)
    {
        AssembledPayload? complete;
        try
        {
            complete = assemblers.GetOrAdd(args.Connection, _ => packetizer.CreateAssembler()).Add(args.Payload);
        }
        catch (Exception ex)
        {
            logger.Record(LogEvents.PacketDropped, "Dropped a packet that could not be assembled: {Message}", ex.Message);
            return;
        }

        if (complete is null)
        {
            return;
        }

        byte[] payload;
        using (complete.Payload) { payload = complete.Payload.Memory.ToArray(); }
        received.Publish(new PeerReceivedEventArgs { Connection = args.Connection, Payload = payload, Packet = complete.FirstPacket });
    }

    private void OnDisconnected(PeerConnectionEventArgs args)
    {
        if (assemblers.TryRemove(args.Connection, out IPacketAssembler? assembler))
        {
            assembler.Dispose();
        }
        if (schedulers.TryRemove(args.Connection, out PacketScheduler? scheduler))
        {
            scheduler.Dispose();
        }
        disconnected.Publish(args);
    }
}
