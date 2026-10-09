namespace BlueHeighliner.Comlink;

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
    /// <param name="frame">The original frame the payload is the serialization of, handed to the host's packet handler and to the packet serializer with each packet.</param>
    /// <returns>The packets, in the order they should be queued. The caller owns them and must dispose each once sent.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The payload is too large to send.</exception>
    IReadOnlyList<Packet> Split(ReadOnlyMemory<byte> payload, int priority, object frame);

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
    /// <param name="engineController">Supplies the packet type, its serializer and field members, and the payload size.</param>
    /// <param name="maxFrameSize">The largest payload, in bytes, that will be sent or reassembled; bounds what a remote sender can make this node allocate. Defaults to 64 MiB.</param>
    /// <param name="maxPendingFrames">How many partly received payloads an assembler keeps at once before it drops the oldest. Defaults to 32.</param>
    /// <exception cref="ArgumentOutOfRangeException">A limit, or the payload size, is too small to be usable.</exception>
    /// <exception cref="InvalidOperationException">The engine controller has no packet type.</exception>
    public Packetizer(IEngineController engineController, int maxFrameSize = 64 * 1024 * 1024, int maxPendingFrames = 32)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFrameSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPendingFrames);
        this.engineController = engineController;
        this.maxFrameSize = maxFrameSize;
        this.maxPendingFrames = maxPendingFrames;
        serializer = engineController.PacketSerializer ?? throw new InvalidOperationException("Packetization needs a PacketType and PacketSerializer, but the engine controller has none");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(engineController.MaxPayloadSize, nameof(engineController.MaxPayloadSize));
        chunkSize = engineController.MaxPayloadSize;
    }

    private readonly IEngineController engineController;
    private readonly IPacketSerializer serializer;
    private readonly int maxFrameSize;
    private readonly int maxPendingFrames;
    private readonly int chunkSize;

    /// <inheritdoc />
    public IReadOnlyList<Packet> Split(ReadOnlyMemory<byte> payload, int priority, object frame)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(payload.Length, maxFrameSize, nameof(payload));

        int count = Math.Max(1, (int)(((long)payload.Length + chunkSize - 1) / chunkSize));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxPacketCount, nameof(payload));

        string? frameId = null;
        List<Packet> packets = new(count);
        try
        {
            for (int index = 0; index < count; index++)
            {
                int offset = index * chunkSize;
                object packet = Build(frame, index, count, payload.Length, payload.Slice(offset, Math.Min(chunkSize, payload.Length - offset)));
                string id = engineController.GetFrameId(packet);
                if (frameId is not null && id != frameId)
                {
                    packet.TryDispose();
                    throw new InvalidOperationException($"The packet handler gave the packets of one frame different frame ids ({frameId} and {id}); it must generate one id per frame");
                }

                frameId = id;
                IMemoryOwner<byte> data;
                try
                {
                    engineController.FrameSerializer.ConfigurePacket(frame, packet);
                    data = serializer.Serialize(packet, frame);
                }
                finally
                {
                    packet.TryDispose();
                }

                packets.Add(new Packet { Data = data, Priority = priority });
            }
        }
        catch
        {
            foreach (Packet packet in packets)
            {
                packet.Dispose();
            }
            throw;
        }

        return packets;
    }

    /// <inheritdoc />
    public IPacketAssembler CreateAssembler() => new PacketAssembler(engineController, maxFrameSize, maxPendingFrames);

    private object Build(object frame, int index, int count, int length, ReadOnlyMemory<byte> data) => engineController.CreateFramePacket(frame, index, count, length, data);
}
