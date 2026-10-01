namespace BlueHeighliner.Comlink.Sample;

/// <summary>Treats a <see cref="SamplePacket"/> as a frame packet when its <see cref="SamplePacket.IsFramePacket"/> flag is set, mapping the engine's packet aspects onto the packet's own differently named fields.</summary>
public sealed class SampleFramePacketHandler : IFramePacketHandler<SamplePacket>
{
    /// <inheritdoc />
    public bool IsValid(SamplePacket packet) => packet.IsFramePacket;

    /// <inheritdoc />
    public SamplePacket Create(FramePacketCreateContext context)
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
    public int GetPayloadId(SamplePacket packet) => packet.Group;

    /// <inheritdoc />
    public int GetIndex(SamplePacket packet) => packet.Position;

    /// <inheritdoc />
    public int GetCount(SamplePacket packet) => packet.Total;

    /// <inheritdoc />
    public int GetPayloadLength(SamplePacket packet) => packet.FullLength;

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetData(SamplePacket packet) => packet.Chunk;
}
