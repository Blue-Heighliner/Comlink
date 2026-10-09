namespace BlueHeighliner.Comlink;

/// <summary>The engine-side form of a packet mapping: every accessor takes the packet as an <see cref="object"/>.</summary>
internal sealed class PacketMap
{
    /// <summary>The host's packet type.</summary>
    public required Type Type { get; init; }
    /// <summary>The serializer for the packet type.</summary>
    public required ServiceRegistration<IPacketSerializer> Serializer { get; init; }
    /// <summary>Gets how the host's packet handler is instantiated.</summary>
    public required ServiceRegistration<IPacketAdapter> Handler { get; init; }
    /// <summary>Gets the heartbeat packet handler, or <see langword="null"/> when none is stated.</summary>
    public ServiceRegistration<IHeartbeatItemHandler>? Heartbeat { get; init; }
}
