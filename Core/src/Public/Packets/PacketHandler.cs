namespace BlueHeighliner.Comlink;

/// <summary>
/// Handles the packets of the host's packet type <typeparamref name="TPacket"/> that carry a piece of a serialized frame, as opposed to a packet that carries no frame
/// (such as one a handshake handler sends). The engine cuts every serialized frame into packets, calls <see cref="IFrameSerializer.ConfigurePacket"/> on each with its frame,
/// and reassembles frames from the packets it receives. State one with <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Packets{THandler}"/>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type.</typeparam>
public interface IPacketHandler<TFrame, TPacket> where TFrame : class where TPacket : class
{
    /// <summary>Returns whether <paramref name="packet"/> carries a piece of a frame.</summary>
    /// <param name="packet">The packet to classify.</param>
    bool IsFramePacket(TPacket packet);

    /// <summary>
    /// Creates a new packet carrying <paramref name="context"/>, for which <see cref="IsFramePacket"/> returns <see langword="true"/>. The engine calls it once for each packet of a frame, in order, and every
    /// packet of one frame must have the same frame id (see <see cref="GetFrameId"/>), which is the handler's to generate: unique among the frames in flight over a connection, and derived from
    /// <see cref="FramePacketCreateContext{TFrame}.Frame"/> (the same instance for every packet of one frame) or from state the handler keeps, so that the packets of one frame agree on it. The engine rejects a frame whose packets disagree.
    /// </summary>
    /// <param name="context">The piece of the frame and where it belongs.</param>
    /// <returns>The new packet.</returns>
    TPacket CreateFramePacket(FramePacketCreateContext<TFrame> context);

    /// <summary>Gets the identifier shared by every packet of one frame from <paramref name="packet"/>. Two frames in flight over one connection must not share one.</summary>
    /// <param name="packet">The packet.</param>
    string GetFrameId(TPacket packet);

    /// <summary>Gets the zero-based position of <paramref name="packet"/> among the packets of its frame.</summary>
    /// <param name="packet">The packet.</param>
    int GetIndex(TPacket packet);

    /// <summary>Gets how many packets the frame <paramref name="packet"/> belongs to was broken into.</summary>
    /// <param name="packet">The packet.</param>
    int GetCount(TPacket packet);

    /// <summary>Gets the length in bytes of the whole serialized frame <paramref name="packet"/> belongs to.</summary>
    /// <param name="packet">The packet.</param>
    int GetFrameLength(TPacket packet);

    /// <summary>Gets the slice of the serialized frame <paramref name="packet"/> carries.</summary>
    /// <param name="packet">The packet.</param>
    ReadOnlyMemory<byte> GetPayload(TPacket packet);
}
