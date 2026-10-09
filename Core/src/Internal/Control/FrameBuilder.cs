namespace BlueHeighliner.Comlink;

/// <summary>Collects what is stated through <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}"/>, collecting the mappings and turning them into a <see cref="FrameMap"/>.</summary>
internal sealed class FrameBuilder<TFrame, TPriority, TLevel, TAspect> where TFrame : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    private ServiceRegistration<IFrameSerializer> serializer = new(_ => new ProtobufSerializer(typeof(TFrame)));
    private Func<TFrame> create = () => new();
    private ServiceRegistration<IHeartbeatItemHandler>? heartbeat;


    /// <summary>The handshake handler, if stated.</summary>
    public ServiceRegistration<IHandshakeHandler>? HandshakeHandler { get; private set; }

    /// <summary>The handler that carries out the host's protocol, if stated.</summary>
    public ServiceRegistration<IEngineFrameHandler>? FrameHandler { get; private set; }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Heartbeat{THandler}"/>
    public FrameBuilder<TFrame, TPriority, TLevel, TAspect> Heartbeat<THandler>() where THandler : IFrameHeartbeatHandler<TFrame, TPriority>
    {
        heartbeat = ServiceRegistration<IHeartbeatItemHandler>.Of(typeof(THandler), handler => new FrameHeartbeatItemHandler<TFrame, TPriority>((IFrameHeartbeatHandler<TFrame, TPriority>)handler));
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Serializer{TSerializer}"/>
    public FrameBuilder<TFrame, TPriority, TLevel, TAspect> Serializer<TSerializer>() where TSerializer : IFrameSerializer
    {
        serializer = ServiceRegistration<IFrameSerializer>.Of(typeof(TSerializer), instance => (IFrameSerializer)instance);
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Create"/>
    public FrameBuilder<TFrame, TPriority, TLevel, TAspect> Create(Func<TFrame> create)
    {
        this.create = create;
        return this;
    }

    /// <summary>States the handler that carries out the host's protocol.</summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    public FrameBuilder<TFrame, TPriority, TLevel, TAspect> Handler<THandler>() where THandler : IFrameHandler<TFrame, TPriority, TLevel, TAspect>
    {
        FrameHandler = ServiceRegistration<IEngineFrameHandler>.Of(typeof(THandler), handler => new EngineFrameHandler<TFrame, TPriority, TLevel, TAspect>((IFrameHandler<TFrame, TPriority, TLevel, TAspect>)handler));
        return this;
    }


    /// <summary>Builds the engine-side map.</summary>
    public FrameMap Build()
        => new()
        {
            Type = typeof(TFrame),
            Serializer = serializer,
            Create = () => create(),
            Heartbeat = heartbeat
        };

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Handshake{THandler}"/>
    public FrameBuilder<TFrame, TPriority, TLevel, TAspect> Handshake<THandler>() where THandler : IFrameHandshakeHandler<TFrame>
    {
        HandshakeHandler = ServiceRegistration<IHandshakeHandler>.Of(typeof(THandler), handler => new FrameHandshakeHandlerAdapter<TFrame>((IFrameHandshakeHandler<TFrame>)handler));
        return this;
    }
}
