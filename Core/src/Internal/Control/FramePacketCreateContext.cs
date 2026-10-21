namespace BlueHeighliner.Comlink;

/// <summary>The engine's implementation of what a host's <see cref="IPacketHandler{TFrame, TPacket}"/> is handed to create a packet.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
internal sealed record FramePacketCreateContext<TFrame> : IFramePacketCreateContext<TFrame> where TFrame : class
{
    /// <inheritdoc />
    public required TFrame Frame { get; init; }

    /// <inheritdoc />
    public required int Index { get; init; }

    /// <inheritdoc />
    public required int Count { get; init; }

    /// <inheritdoc />
    public required int FrameLength { get; init; }

    /// <inheritdoc />
    public required ReadOnlyMemory<byte> Payload { get; init; }
}
