namespace BlueHeighliner.Comlink.Testing;

/// <summary>A mock of the <see cref="IFramePacketCreateContext{TFrame}"/> an <see cref="IPacketHandler{TFrame, TPacket}"/> is given to create a packet.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public sealed class MockFramePacketCreateContext<TFrame> : Mock<IFramePacketCreateContext<TFrame>> where TFrame : class
{
    /// <summary>Initializes the mock.</summary>
    /// <param name="frame">The frame the packet is created from.</param>
    /// <param name="index">The zero-based position of the packet among the packets of its frame.</param>
    /// <param name="count">How many packets the frame was broken into.</param>
    /// <param name="frameLength">The length in bytes of the whole serialized frame.</param>
    /// <param name="payload">The slice of the serialized frame the packet carries.</param>
    public MockFramePacketCreateContext(TFrame frame, int index = 0, int count = 1, int frameLength = 0, ReadOnlyMemory<byte> payload = default)
    {
        Frame = frame;
        Index = index;
        Count = count;
        FrameLength = frameLength;
        Payload = payload;
        Setup(context => context.Frame).Returns(() => Frame);
        Setup(context => context.Index).Returns(() => Index);
        Setup(context => context.Count).Returns(() => Count);
        Setup(context => context.FrameLength).Returns(() => FrameLength);
        Setup(context => context.Payload).Returns(() => Payload);
    }

    /// <summary>Gets or sets what <see cref="IFramePacketCreateContext{TFrame}.Frame"/> returns.</summary>
    public TFrame Frame { get; set; }

    /// <summary>Gets or sets what <see cref="IFramePacketCreateContext{TFrame}.Index"/> returns.</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets what <see cref="IFramePacketCreateContext{TFrame}.Count"/> returns.</summary>
    public int Count { get; set; }

    /// <summary>Gets or sets what <see cref="IFramePacketCreateContext{TFrame}.FrameLength"/> returns.</summary>
    public int FrameLength { get; set; }

    /// <summary>Gets or sets what <see cref="IFramePacketCreateContext{TFrame}.Payload"/> returns.</summary>
    public ReadOnlyMemory<byte> Payload { get; set; }
}
