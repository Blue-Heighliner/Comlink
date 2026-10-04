namespace BlueHeighliner.Comlink;

/// <summary>Collects what is stated through <see cref="IPacketBuilder{TFrame, TPacket, TPriority, TLevel}"/>, collecting the mappings and turning them into a <see cref="PacketMap"/>.</summary>
internal sealed class PacketBuilder<TPacket, TPriority> where TPacket : class, new() where TPriority : struct, Enum
{
    private ServiceRegistration<IPacketSerializer> serializer = new(_ => new ProtobufSerializer(typeof(TPacket)));
    private ServiceRegistration<IFramePacketAdapter>? framePacket;
    private ServiceRegistration<IHeartbeatFrameHandler>? heartbeat;

    /// <summary>The initial packet processor, if stated.</summary>
    public ServiceRegistration<IInitialProcessor>? Initial { get; private set; }

    /// <inheritdoc cref="IPacketBuilder{TFrame, TPacket, TPriority, TLevel}.Frame{THandler}"/>
    public PacketBuilder<TPacket, TPriority> Frame<THandler>() where THandler : IFramePacketHandler<TPacket>
    {
        framePacket = ServiceRegistration<IFramePacketAdapter>.Of(typeof(THandler), handler => new FramePacketAdapter<TPacket>((IFramePacketHandler<TPacket>)handler));
        return this;
    }

    /// <inheritdoc cref="IPacketBuilder{TFrame, TPacket, TPriority, TLevel}.Serializer{TSerializer}"/>
    public PacketBuilder<TPacket, TPriority> Serializer<TSerializer>() where TSerializer : IPacketSerializer
    {
        serializer = ServiceRegistration<IPacketSerializer>.Of(typeof(TSerializer), instance => (IPacketSerializer)instance);
        return this;
    }

    /// <inheritdoc cref="IPacketBuilder{TFrame, TPacket, TPriority, TLevel}.InitialProcessor{TProcessor}"/>
    public PacketBuilder<TPacket, TPriority> InitialProcessor<TProcessor>() where TProcessor : IInitialPacketProcessor<TPacket>
    {
        Initial = ServiceRegistration<IInitialProcessor>.Of(typeof(TProcessor), processor => new InitialPacketProcessorAdapter<TPacket>((IInitialPacketProcessor<TPacket>)processor));
        return this;
    }

    /// <inheritdoc cref="IPacketBuilder{TFrame, TPacket, TPriority, TLevel}.Heartbeat{THandler}"/>
    public PacketBuilder<TPacket, TPriority> Heartbeat<THandler>() where THandler : IHeartbeatHandler<TPacket, TPriority>
    {
        heartbeat = ServiceRegistration<IHeartbeatFrameHandler>.Of(typeof(THandler), handler => new HeartbeatFrameHandler<TPacket, TPriority>((IHeartbeatHandler<TPacket, TPriority>)handler));
        return this;
    }

    /// <summary>Builds the engine-side map.</summary>
    /// <exception cref="InvalidOperationException">The frame packet handler has not been stated.</exception>
    public PacketMap Build()
    {
        if (framePacket is null) { throw new InvalidOperationException($"The packet mapping for {typeof(TPacket).Name} does not state: {nameof(Frame)}"); }

        return new PacketMap
        {
            Type = typeof(TPacket),
            Serializer = serializer,
            FramePacket = framePacket,
            Heartbeat = heartbeat
        };
    }
}
