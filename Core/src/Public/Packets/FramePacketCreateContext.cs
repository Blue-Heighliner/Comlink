namespace BlueHeighliner.Comlink;

/// <summary>What the engine hands to <see cref="IPacketHandler{TFrame, TPacket}.CreateFramePacket"/> to build a packet: one piece of a serialized frame.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface IFramePacketCreateContext<TFrame> where TFrame : class
{
    /// <summary>Gets the frame the packet is being created from. The handler may use it to give every packet of one frame the same frame id.</summary>
    TFrame Frame { get; }

    /// <summary>Gets the zero-based position of the packet among the packets of its frame.</summary>
    int Index { get; }

    /// <summary>Gets how many packets the frame was broken into.</summary>
    int Count { get; }

    /// <summary>Gets the length in bytes of the whole serialized frame.</summary>
    int FrameLength { get; }

    /// <summary>Gets the slice of the serialized frame the packet carries. It is only valid for the duration of the call, so a packet that stores it must copy it.</summary>
    ReadOnlyMemory<byte> Payload { get; }
}
