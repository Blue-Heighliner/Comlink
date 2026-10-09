namespace BlueHeighliner.Comlink;

/// <summary>The engine's untyped view of the host's <see cref="IPacketHandler{TFrame, TPacket}"/>, working on frames and packets as <see cref="object"/>.</summary>
internal interface IPacketAdapter
{
    /// <summary>Returns whether <paramref name="packet"/> carries a piece of a frame.</summary>
    /// <param name="packet">The packet.</param>
    bool IsFramePacket(object packet);

    /// <summary>Creates a packet carrying one piece of a serialized frame.</summary>
    /// <param name="frame">The frame the packet is being created from, an instance of the host's frame type.</param>
    /// <param name="index">The position of the packet among the packets of its frame.</param>
    /// <param name="count">How many packets the frame was broken into.</param>
    /// <param name="frameLength">The length in bytes of the whole serialized frame.</param>
    /// <param name="payload">The slice of the serialized frame the packet carries.</param>
    object CreateFramePacket(object frame, int index, int count, int frameLength, ReadOnlyMemory<byte> payload);

    /// <summary>Gets the frame identifier of <paramref name="packet"/>.</summary>
    /// <param name="packet">The packet.</param>
    string GetFrameId(object packet);

    /// <summary>Gets the position of <paramref name="packet"/> among the packets of its frame.</summary>
    /// <param name="packet">The packet.</param>
    int GetIndex(object packet);

    /// <summary>Gets how many packets the frame of <paramref name="packet"/> was broken into.</summary>
    /// <param name="packet">The packet.</param>
    int GetCount(object packet);

    /// <summary>Gets the length in bytes of the whole serialized frame of <paramref name="packet"/>.</summary>
    /// <param name="packet">The packet.</param>
    int GetFrameLength(object packet);

    /// <summary>Gets the slice of the serialized frame <paramref name="packet"/> carries.</summary>
    /// <param name="packet">The packet.</param>
    ReadOnlyMemory<byte> GetPayload(object packet);
}

/// <summary>Adapts a typed <see cref="IPacketHandler{TFrame, TPacket}"/> to <see cref="IPacketAdapter"/>.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type.</typeparam>
/// <param name="handler">The host's handler.</param>
internal sealed class PacketAdapter<TFrame, TPacket>(IPacketHandler<TFrame, TPacket> handler) : IPacketAdapter where TFrame : class where TPacket : class
{
    /// <inheritdoc />
    public bool IsFramePacket(object packet) => handler.IsFramePacket((TPacket)packet);

    /// <inheritdoc />
    public object CreateFramePacket(object frame, int index, int count, int frameLength, ReadOnlyMemory<byte> payload)
        => handler.CreateFramePacket(new FramePacketCreateContext<TFrame> { Frame = (TFrame)frame, Index = index, Count = count, FrameLength = frameLength, Payload = payload });

    /// <inheritdoc />
    public string GetFrameId(object packet) => handler.GetFrameId((TPacket)packet);

    /// <inheritdoc />
    public int GetIndex(object packet) => handler.GetIndex((TPacket)packet);

    /// <inheritdoc />
    public int GetCount(object packet) => handler.GetCount((TPacket)packet);

    /// <inheritdoc />
    public int GetFrameLength(object packet) => handler.GetFrameLength((TPacket)packet);

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetPayload(object packet) => handler.GetPayload((TPacket)packet);
}
