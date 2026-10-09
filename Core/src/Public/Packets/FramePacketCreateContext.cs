namespace BlueHeighliner.Comlink;

/// <summary>What the engine hands to <see cref="IPacketHandler{TFrame, TPacket}.CreateFramePacket"/> to build a packet: one piece of a serialized frame.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public sealed record FramePacketCreateContext<TFrame> where TFrame : class
{
    /// <summary>Gets the frame the packet is being created from. The handler may use it to give every packet of one frame the same frame id.</summary>
    public required TFrame Frame { get; init; }

    /// <summary>Gets the zero-based position of the packet among the packets of its frame.</summary>
    public required int Index { get; init; }

    /// <summary>Gets how many packets the frame was broken into.</summary>
    public required int Count { get; init; }

    /// <summary>Gets the length in bytes of the whole serialized frame.</summary>
    public required int FrameLength { get; init; }

    /// <summary>Gets the slice of the serialized frame the packet carries. It is only valid for the duration of the call, so a packet that stores it must copy it.</summary>
    public required ReadOnlyMemory<byte> Payload { get; init; }
}
