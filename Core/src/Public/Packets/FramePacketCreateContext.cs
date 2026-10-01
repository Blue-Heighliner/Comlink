namespace BlueHeighliner.Comlink;

/// <summary>What the engine hands to <see cref="IFramePacketHandler{TPacket}.Create"/> to build a frame packet: one piece of a serialized frame.</summary>
public sealed record FramePacketCreateContext
{
    /// <summary>Gets the identifier shared by every packet of one payload, which tells packets of different payloads apart.</summary>
    public required int PayloadId { get; init; }

    /// <summary>Gets the zero-based position of the packet among the packets of its payload.</summary>
    public required int Index { get; init; }

    /// <summary>Gets how many packets the payload was broken into.</summary>
    public required int Count { get; init; }

    /// <summary>Gets the length in bytes of the whole payload.</summary>
    public required int PayloadLength { get; init; }

    /// <summary>Gets the slice of the payload the packet carries. It is only valid for the duration of the call, so a packet that stores it must copy it.</summary>
    public required ReadOnlyMemory<byte> Data { get; init; }
}
