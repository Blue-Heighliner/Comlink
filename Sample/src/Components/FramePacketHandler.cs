namespace BlueHeighliner.Comlink.Sample;

/// <summary>Treats a <see cref="Packet"/> as a frame packet when its <see cref="Packet.IsFramePacket"/> flag is set, mapping the engine's packet aspects onto the packet's own differently named fields.</summary>
public sealed class FramePacketHandler : IFramePacketHandler<Packet>
{
    /// <inheritdoc />
    public bool IsValid(Packet packet) => packet.IsFramePacket;

    /// <inheritdoc />
    public Packet Create(FramePacketCreateContext context)
        => new()
        {
            IsFramePacket = true,
            Group = context.PayloadId,
            Position = context.Index,
            Total = context.Count,
            FullLength = context.PayloadLength,
            Chunk = context.Data.ToArray()
        };

    /// <inheritdoc />
    public int GetPayloadId(Packet packet) => packet.Group;

    /// <inheritdoc />
    public int GetIndex(Packet packet) => packet.Position;

    /// <inheritdoc />
    public int GetCount(Packet packet) => packet.Total;

    /// <inheritdoc />
    public int GetPayloadLength(Packet packet) => packet.FullLength;

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetData(Packet packet) => packet.Chunk;
}
