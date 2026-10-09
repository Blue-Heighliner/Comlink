namespace BlueHeighliner.Comlink;

/// <summary>Collects what is stated through <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}"/>, collecting the mappings and turning them into a <see cref="FrameMap"/>.</summary>
internal sealed class FrameBuilder<TFrame, TPriority, TLevel, TAspect> where TFrame : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    private ServiceRegistration<IFrameSerializer> serializer = new(_ => new ProtobufSerializer(typeof(TFrame)));
    private readonly List<AutoForwarderDefinition> autoForwarders = [];
    private Func<TFrame> create = () => new();
    private ServiceRegistration<IHeartbeatFrameHandler>? heartbeat;


    /// <summary>The processor that carries out the host's protocol, if stated.</summary>
    public ServiceRegistration<INetworkHandler>? NetworkHandler { get; private set; }

    /// <summary>The auto forwarders.</summary>
    public IReadOnlyList<AutoForwarderDefinition> AutoForwarders => autoForwarders;

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.AutoForwarder"/>
    public FrameBuilder<TFrame, TPriority, TLevel, TAspect> AutoForwarder(string name)
    {
        if (autoForwarders.All(forwarder => forwarder.Name != name))
        {
            autoForwarders.Add(new() { Name = name });
        }

        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Heartbeat{THandler}"/>
    public FrameBuilder<TFrame, TPriority, TLevel, TAspect> Heartbeat<THandler>() where THandler : IHeartbeatHandler<TFrame, TPriority>
    {
        heartbeat = ServiceRegistration<IHeartbeatFrameHandler>.Of(typeof(THandler), handler => new HeartbeatFrameHandler<TFrame, TPriority>((IHeartbeatHandler<TFrame, TPriority>)handler));
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

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Processor{TProcessor}"/>
    public FrameBuilder<TFrame, TPriority, TLevel, TAspect> Processor<TProcessor>() where TProcessor : INetworkProcessor<TFrame, TPriority, TLevel, TAspect>
    {
        NetworkHandler = ServiceRegistration<INetworkHandler>.Of(typeof(TProcessor), processor => new NetworkHandler<TFrame, TPriority, TLevel, TAspect>((INetworkProcessor<TFrame, TPriority, TLevel, TAspect>)processor));
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
}
