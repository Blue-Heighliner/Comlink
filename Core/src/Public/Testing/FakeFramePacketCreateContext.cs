namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="IFramePacketCreateContext{TFrame}"/> an <see cref="IPacketHandler{TFrame, TPacket}"/> is handed for each piece of a frame, to test a packet handler without the engine.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <param name="frame">The frame being cut into packets.</param>
/// <param name="index">The zero-based position of the piece among the frame's pieces.</param>
/// <param name="count">How many pieces the frame is cut into.</param>
/// <param name="frameLength">The length in bytes of the whole serialized frame.</param>
/// <param name="payload">The bytes of the frame this piece carries.</param>
public sealed class FakeFramePacketCreateContext<TFrame>(TFrame frame, int index = 0, int count = 1, int frameLength = 0, ReadOnlyMemory<byte> payload = default) : IFramePacketCreateContext<TFrame> where TFrame : class
{
    /// <inheritdoc />
    public TFrame Frame { get; set; } = frame;

    /// <inheritdoc />
    public int Index { get; set; } = index;

    /// <inheritdoc />
    public int Count { get; set; } = count;

    /// <inheritdoc />
    public int FrameLength { get; set; } = frameLength;

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Payload { get; set; } = payload;
}
