namespace BlueHeighliner.Comlink.Tests;

/// <summary>
/// Test <see cref="IPacketHandler{TFrame, TPacket}"/> for <see cref="TestPacket"/>, recognizing packets with <see cref="TestPacket.IsFramePacket"/> set. It gives a frame a new id when its first packet
/// is created and reuses it for the rest, remembering it by the frame instance.
/// </summary>
public sealed class TestPacketHandler : IPacketHandler<TestFrame, TestPacket>
{
    private static readonly ConditionalWeakTable<TestFrame, string> ids = [];
    private static int counter;

    /// <inheritdoc />
    public bool IsFramePacket(TestPacket packet) => packet.IsFramePacket;

    /// <inheritdoc />
    public TestPacket CreateFramePacket(FramePacketCreateContext<TestFrame> context)
        => new()
        {
            IsFramePacket = true,
            PayloadId = int.Parse(ids.GetValue(context.Frame, _ => Interlocked.Increment(ref counter).ToString())),
            Index = context.Index,
            Count = context.Count,
            PayloadLength = context.FrameLength,
            Data = context.Payload.ToArray()
        };

    /// <inheritdoc />
    public string GetFrameId(TestPacket packet) => packet.PayloadId.ToString();

    /// <inheritdoc />
    public int GetIndex(TestPacket packet) => packet.Index;

    /// <inheritdoc />
    public int GetCount(TestPacket packet) => packet.Count;

    /// <inheritdoc />
    public int GetFrameLength(TestPacket packet) => packet.PayloadLength;

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetPayload(TestPacket packet) => packet.Data;
}
