namespace BlueHeighliner.Comlink;

/// <summary>Implements <see cref="IPacketBuilder{TPacket}"/>, collecting the mappings and turning them into a <see cref="PacketMap"/>.</summary>
internal sealed class PacketBuilder<TPacket> : IPacketBuilder<TPacket> where TPacket : class, new()
{
    private ServiceRegistration<IPacketSerializer> serializer = new(_ => new ProtobufSerializer(typeof(TPacket)));
    private ServiceRegistration<IFramePacketAdapter>? framePacket;
    private int size = 16 * 1024;
    private int window = 1;

    /// <summary>The initial packet processor, if stated.</summary>
    public ServiceRegistration<IInitialProcessor>? Initial { get; private set; }

    /// <inheritdoc />
    public IPacketBuilder<TPacket> Frame<THandler>() where THandler : IFramePacketHandler<TPacket>
    {
        framePacket = ServiceRegistration<IFramePacketAdapter>.Of(typeof(THandler), handler => new FramePacketAdapter<TPacket>((IFramePacketHandler<TPacket>)handler));
        return this;
    }

    /// <inheritdoc />
    public IPacketBuilder<TPacket> Size(int bytes)
    {
        size = bytes;
        return this;
    }

    /// <inheritdoc />
    public IPacketBuilder<TPacket> Window(int packets)
    {
        window = packets;
        return this;
    }

    /// <inheritdoc />
    public IPacketBuilder<TPacket> Serializer<TSerializer>() where TSerializer : IPacketSerializer
    {
        serializer = ServiceRegistration<IPacketSerializer>.Of(typeof(TSerializer), instance => (IPacketSerializer)instance);
        return this;
    }

    /// <inheritdoc />
    public IPacketBuilder<TPacket> InitialProcessor<TProcessor>() where TProcessor : IInitialPacketProcessor<TPacket>
    {
        Initial = ServiceRegistration<IInitialProcessor>.Of(typeof(TProcessor), processor => new InitialPacketProcessorAdapter<TPacket>((IInitialPacketProcessor<TPacket>)processor));
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
            Size = size,
            Window = window,
            FramePacket = framePacket
        };
    }
}
