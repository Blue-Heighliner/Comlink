namespace BlueHeighliner.Comlink;

/// <summary>Collects what is stated through <see cref="IPacketBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}"/>, collecting the mappings and turning them into a <see cref="PacketMap"/>.</summary>
internal sealed class PacketBuilder<TFrame, TPacket, TPriority> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum
{
    private ServiceRegistration<IPacketSerializer> serializer = new(_ => new ProtobufSerializer(typeof(TPacket)));
    private ServiceRegistration<IPacketAdapter>? packetHandler;
    private ServiceRegistration<IHeartbeatItemHandler>? heartbeat;

    /// <summary>The handshake processor, if stated.</summary>
    public ServiceRegistration<IHandshakeHandler>? HandshakeHandler { get; private set; }

    /// <summary>States the handler for the packets that carry a piece of a serialized frame.</summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    public PacketBuilder<TFrame, TPacket, TPriority> Handler<THandler>() where THandler : IPacketHandler<TFrame, TPacket>
    {
        packetHandler = ServiceRegistration<IPacketAdapter>.Of(typeof(THandler), handler => new PacketAdapter<TFrame, TPacket>((IPacketHandler<TFrame, TPacket>)handler));
        return this;
    }

    /// <inheritdoc cref="IPacketBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Serializer{TSerializer}"/>
    public PacketBuilder<TFrame, TPacket, TPriority> Serializer<TSerializer>() where TSerializer : IPacketSerializer
    {
        serializer = ServiceRegistration<IPacketSerializer>.Of(typeof(TSerializer), instance => (IPacketSerializer)instance);
        return this;
    }

    /// <inheritdoc cref="IPacketBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Handshake{TProcessor}"/>
    public PacketBuilder<TFrame, TPacket, TPriority> Handshake<TProcessor>() where TProcessor : IPacketHandshakeProcessor<TPacket>
    {
        HandshakeHandler = ServiceRegistration<IHandshakeHandler>.Of(typeof(TProcessor), processor => new PacketHandshakeProcessorAdapter<TPacket>((IPacketHandshakeProcessor<TPacket>)processor));
        return this;
    }

    /// <inheritdoc cref="IPacketBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Heartbeat{THandler}"/>
    public PacketBuilder<TFrame, TPacket, TPriority> Heartbeat<THandler>() where THandler : IPacketHeartbeatHandler<TPacket, TPriority>
    {
        heartbeat = ServiceRegistration<IHeartbeatItemHandler>.Of(typeof(THandler), handler => new PacketHeartbeatItemHandler<TPacket, TPriority>((IPacketHeartbeatHandler<TPacket, TPriority>)handler));
        return this;
    }

    /// <summary>Builds the engine-side map.</summary>
    /// <exception cref="InvalidOperationException">The packet handler has not been stated.</exception>
    public PacketMap Build()
    {
        if (packetHandler is null)
        {
            throw new InvalidOperationException($"The packet mapping for {typeof(TPacket).Name} does not state: {nameof(Handler)}");
        }

        return new PacketMap
        {
            Type = typeof(TPacket),
            Serializer = serializer,
            Handler = packetHandler,
            Heartbeat = heartbeat
        };
    }
}
