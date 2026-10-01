namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IFramePacketHandler{TPacket}"/> for <see cref="TestPacket"/>, recognizing packets with <see cref="TestPacket.IsFramePacket"/> set.</summary>
public sealed class TestFramePacketHandler : IFramePacketHandler<TestPacket>
{
    /// <inheritdoc />
    public bool IsValid(TestPacket packet) => packet.IsFramePacket;

    /// <inheritdoc />
    public TestPacket Create(FramePacketCreateContext context)
        => new()
        {
            IsFramePacket = true,
            PayloadId = context.PayloadId,
            Index = context.Index,
            Count = context.Count,
            PayloadLength = context.PayloadLength,
            Data = context.Data.ToArray()
        };

    /// <inheritdoc />
    public int GetPayloadId(TestPacket packet) => packet.PayloadId;

    /// <inheritdoc />
    public int GetIndex(TestPacket packet) => packet.Index;

    /// <inheritdoc />
    public int GetCount(TestPacket packet) => packet.Count;

    /// <inheritdoc />
    public int GetPayloadLength(TestPacket packet) => packet.PayloadLength;

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetData(TestPacket packet) => packet.Data;
}
