namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// Breaks a payload into prioritized packets that are sent over the network in its place, and creates the
/// assemblers that put the packets a remote node sent back into payloads. Sending a large payload as several
/// packets lets a higher-priority payload queued meanwhile go out between them instead of waiting behind the
/// whole thing.
/// </summary>
internal interface IPacketizer
{
    /// <summary>Breaks <paramref name="payload"/> into packets. Always returns at least one packet, so an empty payload still produces a packet a receiver can tell apart from silence.</summary>
    /// <param name="payload">The payload to send, already serialized.</param>
    /// <param name="priority">The payload's own send priority, which every packet inherits.</param>
    /// <returns>The packets, in the order they should be queued. The caller owns them and must dispose each once sent.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The payload is too large to send.</exception>
    IReadOnlyList<Packet> Split(ReadOnlyMemory<byte> payload, int priority);

    /// <summary>Creates the state one remote sender's packets are reassembled with. Each sender needs its own, so that packets of different senders never mix.</summary>
    /// <returns>A new assembler, owned by the caller.</returns>
    IPacketAssembler CreateAssembler();
}

/// <summary>
/// The standard <see cref="IPacketizer"/>. The engine owns all of the packetization logic; what a packet looks
/// like is the host's, through <see cref="IEngineController.PacketType"/>: each packet is an instance of it, filled
/// in and read back through the controller's packet field members and serialized by its
/// <see cref="IEngineController.PacketSerializer"/>. A payload is cut into consecutive chunks, all of one size
/// except possibly the last, each carried in one packet together with the payload's id, the packet's index and
/// count, and the payload's length. Every packet inherits the payload's priority.
/// </summary>
internal sealed class Packetizer : IPacketizer
{
    /// <summary>Gets the most packets one payload may be cut into, which bounds what a remote sender can make a receiver track for it.</summary>
    internal static int MaxPacketCount { get; } = ushort.MaxValue;

    /// <summary>Initializes a packetizer for the packet format <paramref name="engineController"/> describes.</summary>
    /// <param name="engineController">Supplies the packet type, its serializer and field members, and the packet size.</param>
    /// <param name="maxPayloadSize">The largest payload, in bytes, that will be sent or reassembled; bounds what a remote sender can make this node allocate. Defaults to 64 MiB.</param>
    /// <param name="maxPendingPayloads">How many partly received payloads an assembler keeps at once before it drops the oldest. Defaults to 32.</param>
    /// <exception cref="ArgumentOutOfRangeException">A limit is too small to be usable.</exception>
    /// <exception cref="InvalidOperationException">The engine controller has no packet type, or its packet size leaves no room for payload in a packet.</exception>
    public Packetizer(IEngineController engineController, int maxPayloadSize = 64 * 1024 * 1024, int maxPendingPayloads = 32)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPayloadSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPendingPayloads);
        this.engineController = engineController;
        this.maxPayloadSize = maxPayloadSize;
        this.maxPendingPayloads = maxPendingPayloads;
        serializer = engineController.PacketSerializer ?? throw new InvalidOperationException("Packetization needs a PacketType and PacketSerializer, but the engine controller has none");
        packetSize = engineController.PacketSize;
        chunkSize = MeasureChunkSize();
        nextId = Random.Shared.Next();
    }

    private readonly IEngineController engineController;
    private readonly INetworkSerializer serializer;
    private readonly int maxPayloadSize;
    private readonly int maxPendingPayloads;
    private readonly int packetSize;
    private readonly int chunkSize;
    private int nextId;

    /// <inheritdoc />
    public IReadOnlyList<Packet> Split(ReadOnlyMemory<byte> payload, int priority)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(payload.Length, maxPayloadSize, nameof(payload));

        int count = Math.Max(1, (int)(((long)payload.Length + chunkSize - 1) / chunkSize));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxPacketCount, nameof(payload));

        int id = Interlocked.Increment(ref nextId) & int.MaxValue;
        List<Packet> packets = new(count);
        try
        {
            for (int index = 0; index < count; index++)
            {
                int offset = index * chunkSize;
                object packet = Build(id, index, count, payload.Length, payload.Slice(offset, Math.Min(chunkSize, payload.Length - offset)));
                IMemoryOwner<byte> data = serializer.Serialize(packet);
                if (data.Memory.Length > packetSize)
                {
                    int length = data.Memory.Length;
                    data.Dispose();
                    throw new InvalidOperationException($"A serialized packet came to {length} bytes, over the PacketSize of {packetSize}");
                }

                packets.Add(new Packet { Data = data, Priority = priority });
            }
        }
        catch
        {
            foreach (Packet packet in packets) { packet.Dispose(); }
            throw;
        }

        return packets;
    }

    /// <inheritdoc />
    public IPacketAssembler CreateAssembler() => new PacketAssembler(engineController, maxPayloadSize, maxPendingPayloads);

    private object Build(int id, int index, int count, int length, ReadOnlyMemory<byte> data)
    {
        object packet = engineController.CreatePacket();
        engineController.SetPayloadId(packet, id);
        engineController.SetPacketIndex(packet, index);
        engineController.SetPacketCount(packet, count);
        engineController.SetPayloadLength(packet, length);
        engineController.SetPacketData(packet, data);
        return packet;
    }

    // What a serializer makes of a packet is the host's business, so how much payload fits under the packet size is
    // measured rather than assumed, by searching for the largest chunk whose serialized packet fits. The probe
    // carries the largest possible field values, so the packets actually sent, whose fields are smaller, never come
    // out bigger. A format that grows the data (a text encoding, say) needs no special handling.
    private int MeasureChunkSize()
    {
        if (Probe(1) > packetSize) { throw new InvalidOperationException($"PacketSize {packetSize} leaves no room for payload in a packet of this format"); }

        int fits = 1;
        int over = Math.Min(packetSize, maxPayloadSize) + 1;
        while (over - fits > 1)
        {
            int middle = fits + ((over - fits) / 2);
            if (Probe(middle) <= packetSize) { fits = middle; }
            else { over = middle; }
        }

        return fits;
    }

    private int Probe(int dataLength)
    {
        object packet = Build(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, new byte[dataLength]);
        using IMemoryOwner<byte> serialized = serializer.Serialize(packet);
        return serialized.Memory.Length;
    }
}
