namespace BlueHeighliner.Comlink;

/// <summary>
/// Handles the packets of the host's packet type <typeparamref name="TPacket"/> that are frame packets: the ones that carry a piece of a serialized frame, as opposed to
/// a packet that carries no frame (such as one an initial packet processor exchanges). The engine cuts every payload into frame packets, calls
/// <see cref="IFrameSerializer.ConfigurePacket"/> on each with its frame, and reassembles payloads from the frame packets it receives. See <see cref="IPacketBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Frame{THandler}"/>.
/// </summary>
/// <typeparam name="TPacket">The host's packet type.</typeparam>
public interface IFramePacketHandler<TPacket> where TPacket : class
{
    /// <summary>Returns whether <paramref name="packet"/> is a frame packet.</summary>
    /// <param name="packet">The packet to classify.</param>
    bool IsValid(TPacket packet);

    /// <summary>Creates a new frame packet carrying <paramref name="context"/>, for which <see cref="IsValid"/> returns <see langword="true"/>.</summary>
    /// <param name="context">The piece of the payload and where it belongs.</param>
    /// <returns>The new packet.</returns>
    TPacket Create(FramePacketCreateContext context);

    /// <summary>Gets the identifier shared by every packet of one payload from <paramref name="packet"/>.</summary>
    int GetPayloadId(TPacket packet);

    /// <summary>Gets the zero-based position of <paramref name="packet"/> among the packets of its payload.</summary>
    int GetIndex(TPacket packet);

    /// <summary>Gets how many packets the payload <paramref name="packet"/> belongs to was broken into.</summary>
    int GetCount(TPacket packet);

    /// <summary>Gets the length in bytes of the whole payload <paramref name="packet"/> belongs to.</summary>
    int GetPayloadLength(TPacket packet);

    /// <summary>Gets the slice of the payload <paramref name="packet"/> carries.</summary>
    ReadOnlyMemory<byte> GetData(TPacket packet);
}
