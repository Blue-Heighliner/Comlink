namespace BlueHeighliner.Comlink.Control;

/// <summary>The engine's untyped view of the host's <see cref="IFramePacketHandler{TPacket}"/>, working on packets as <see cref="object"/>.</summary>
internal interface IFramePacketAdapter
{
    /// <summary>Returns whether <paramref name="packet"/> is a frame packet.</summary>
    bool IsValid(object packet);
    /// <summary>Creates a frame packet carrying <paramref name="context"/>.</summary>
    object Create(FramePacketCreateContext context);
    /// <summary>Gets the payload identifier of <paramref name="packet"/>.</summary>
    int GetPayloadId(object packet);
    /// <summary>Gets the position of <paramref name="packet"/> among the packets of its payload.</summary>
    int GetIndex(object packet);
    /// <summary>Gets how many packets the payload of <paramref name="packet"/> was broken into.</summary>
    int GetCount(object packet);
    /// <summary>Gets the length in bytes of the whole payload of <paramref name="packet"/>.</summary>
    int GetPayloadLength(object packet);
    /// <summary>Gets the slice of the payload <paramref name="packet"/> carries.</summary>
    ReadOnlyMemory<byte> GetData(object packet);
}

/// <summary>Adapts a typed <see cref="IFramePacketHandler{TPacket}"/> to <see cref="IFramePacketAdapter"/>.</summary>
internal sealed class FramePacketAdapter<TPacket>(IFramePacketHandler<TPacket> handler) : IFramePacketAdapter where TPacket : class
{
    /// <inheritdoc />
    public bool IsValid(object packet) => handler.IsValid((TPacket)packet);

    /// <inheritdoc />
    public object Create(FramePacketCreateContext context) => handler.Create(context);

    /// <inheritdoc />
    public int GetPayloadId(object packet) => handler.GetPayloadId((TPacket)packet);

    /// <inheritdoc />
    public int GetIndex(object packet) => handler.GetIndex((TPacket)packet);

    /// <inheritdoc />
    public int GetCount(object packet) => handler.GetCount((TPacket)packet);

    /// <inheritdoc />
    public int GetPayloadLength(object packet) => handler.GetPayloadLength((TPacket)packet);

    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetData(object packet) => handler.GetData((TPacket)packet);
}
