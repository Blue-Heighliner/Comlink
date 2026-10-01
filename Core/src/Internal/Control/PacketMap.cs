namespace BlueHeighliner.Comlink.Control;

/// <summary>The engine-side form of a packet mapping: every accessor takes the packet as an <see cref="object"/>.</summary>
internal sealed class PacketMap
{
    /// <summary>The host's packet type.</summary>
    public required Type Type { get; init; }
    /// <summary>The serializer for the packet type.</summary>
    public required ServiceRegistration<IPacketSerializer> Serializer { get; init; }
    /// <summary>The largest a serialized packet may be, in bytes.</summary>
    public required int Size { get; init; }
    /// <summary>How many packets may be in flight over one connection at once.</summary>
    public required int Window { get; init; }
    /// <summary>Gets how the host's frame packet handler is instantiated.</summary>
    public required ServiceRegistration<IFramePacketAdapter> FramePacket { get; init; }
}
