namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Treats a <see cref="Packet"/> as a frame packet when its <see cref="Packet.IsFramePacket"/> flag is set, mapping the engine's packet aspects onto the packet's own differently named fields.
/// It gives each frame a random frame id the first time one of its packets is created and reuses it for the rest, remembering the id by the frame instance.
/// </summary>
public sealed class PacketHandler : IPacketHandler<Frame, Packet>
{
    private static string NewId() => Guid.NewGuid().ToString("N");

    private readonly ConditionalWeakTable<Frame, string> ids = [];

    /// <inheritdoc />
    public bool IsFramePacket(Packet packet) => packet.IsFramePacket;

    /// <inheritdoc />
    public Packet CreateFramePacket(FramePacketCreateContext<Frame> context)
        => new()
        {
            IsFramePacket = true,
            Group = context.Frame is { } frame ? ids.GetValue(frame, _ => NewId()) : NewId(),
            Position = context.Index,
            Total = context.Count,
            FullLength = context.FrameLength,
            Chunk = context.Payload.ToArray()
        };

    /// <inheritdoc />
    public string GetFrameId(Packet packet) => packet.Group;

    /// <inheritdoc />
    public int GetIndex(Packet packet) => packet.Position;

    /// <inheritdoc />
    public int GetCount(Packet packet) => packet.Total;

    /// <inheritdoc />
    public int GetFrameLength(Packet packet) => packet.FullLength;

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetPayload(Packet packet) => packet.Chunk;
}
