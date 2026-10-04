namespace BlueHeighliner.Comlink;

/// <summary>Collects what is stated through <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}"/>, collecting the mappings and turning them into a <see cref="FrameMap"/>.</summary>
internal sealed class FrameBuilder<TFrame, TPriority, TLevel>(EngineBuilder state) where TFrame : class, new() where TPriority : struct, Enum where TLevel : struct, Enum
{
    private ServiceRegistration<IFrameSerializer> serializer = new(_ => new ProtobufSerializer(typeof(TFrame)));
    private readonly List<ServiceRegistration<AutoForwardControllerDefinition>> autoForwardControllers = [];
    private Func<TFrame> create = () => new();
    private ServiceRegistration<IMessageFrameHandler>? message;
    private ServiceRegistration<IRetrievalFrameHandler>? retrieval;
    private ServiceRegistration<IReceiptFrameHandler>? readReceipt;
    private ServiceRegistration<IReceiptFrameHandler>? receiveReceipt;
    private ServiceRegistration<IHeartbeatFrameHandler>? heartbeat;

    /// <summary>The initial message processor, if stated.</summary>
    public ServiceRegistration<IInitialProcessor>? Initial { get; private set; }

    /// <summary>The processor that reacts to peer activity, if stated.</summary>
    public ServiceRegistration<INetworkHandler>? NetworkHandler { get; private set; }

    /// <summary>The custom auto forward controllers.</summary>
    public IReadOnlyList<ServiceRegistration<AutoForwardControllerDefinition>> AutoForwardControllers => autoForwardControllers;

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.Message{THandler}"/>
    public FrameBuilder<TFrame, TPriority, TLevel> Message<THandler>() where THandler : IMessageHandler<TFrame, TPriority, TLevel>
    {
        message = ServiceRegistration<IMessageFrameHandler>.Of(typeof(THandler), handler => new MessageFrameHandler<TFrame, TPriority, TLevel>((IMessageHandler<TFrame, TPriority, TLevel>)handler, state.SecurityLevelValues));
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.AutoForward{TController}"/>
    public FrameBuilder<TFrame, TPriority, TLevel> AutoForward<TController>() where TController : IAutoForwardController<TFrame>
    {
        autoForwardControllers.Add(ServiceRegistration<AutoForwardControllerDefinition>.Of(typeof(TController), instance =>
        {
            IAutoForwardController<TFrame> controller = (IAutoForwardController<TFrame>)instance;
            return new AutoForwardControllerDefinition { Name = controller.Name, Users = [.. controller.Users], Filter = frame => controller.Accepts((TFrame)frame) };
        }));
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.Retrieval{THandler}"/>
    public FrameBuilder<TFrame, TPriority, TLevel> Retrieval<THandler>() where THandler : IRetrievalHandler<TFrame, TPriority>
    {
        retrieval = ServiceRegistration<IRetrievalFrameHandler>.Of(typeof(THandler), handler => new RetrievalFrameHandler<TFrame, TPriority>((IRetrievalHandler<TFrame, TPriority>)handler));
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.ReadReceipt{THandler}"/>
    public FrameBuilder<TFrame, TPriority, TLevel> ReadReceipt<THandler>() where THandler : IReadReceiptHandler<TFrame, TPriority>
    {
        readReceipt = ServiceRegistration<IReceiptFrameHandler>.Of(typeof(THandler), instance =>
        {
            IReadReceiptHandler<TFrame, TPriority> handler = (IReadReceiptHandler<TFrame, TPriority>)instance;
            return new ReceiptFrameHandler<TFrame>(handler.Priority, handler.IsValid, handler.Create, handler.GetMessageId, handler.GetSender, handler.SetSender, handler.GetDestination);
        });
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.ReceiveReceipt{THandler}"/>
    public FrameBuilder<TFrame, TPriority, TLevel> ReceiveReceipt<THandler>() where THandler : IReceiveReceiptHandler<TFrame, TPriority>
    {
        receiveReceipt = ServiceRegistration<IReceiptFrameHandler>.Of(typeof(THandler), instance =>
        {
            IReceiveReceiptHandler<TFrame, TPriority> handler = (IReceiveReceiptHandler<TFrame, TPriority>)instance;
            return new ReceiptFrameHandler<TFrame>(handler.Priority, handler.IsValid, handler.Create, handler.GetMessageId, handler.GetSender, handler.SetSender, handler.GetDestination);
        });
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.Heartbeat{THandler}"/>
    public FrameBuilder<TFrame, TPriority, TLevel> Heartbeat<THandler>() where THandler : IHeartbeatHandler<TFrame, TPriority>
    {
        heartbeat = ServiceRegistration<IHeartbeatFrameHandler>.Of(typeof(THandler), handler => new HeartbeatFrameHandler<TFrame, TPriority>((IHeartbeatHandler<TFrame, TPriority>)handler));
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.Serializer{TSerializer}"/>
    public FrameBuilder<TFrame, TPriority, TLevel> Serializer<TSerializer>() where TSerializer : IFrameSerializer
    {
        serializer = ServiceRegistration<IFrameSerializer>.Of(typeof(TSerializer), instance => (IFrameSerializer)instance);
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.Create"/>
    public FrameBuilder<TFrame, TPriority, TLevel> Create(Func<TFrame> create)
    {
        this.create = create;
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.Processor{TProcessor}"/>
    public FrameBuilder<TFrame, TPriority, TLevel> Processor<TProcessor>() where TProcessor : INetworkProcessor<TFrame>
    {
        NetworkHandler = ServiceRegistration<INetworkHandler>.Of(typeof(TProcessor), processor => new NetworkProcessorAdapter<TFrame>((INetworkProcessor<TFrame>)processor));
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.InitialProcessor{TProcessor}"/>
    public FrameBuilder<TFrame, TPriority, TLevel> InitialProcessor<TProcessor>() where TProcessor : IInitialFrameProcessor<TFrame>
    {
        Initial = ServiceRegistration<IInitialProcessor>.Of(typeof(TProcessor), processor => new InitialFrameProcessorAdapter<TFrame>((IInitialFrameProcessor<TFrame>)processor));
        return this;
    }

    /// <summary>Builds the engine-side map.</summary>
    /// <exception cref="InvalidOperationException">A logical field or frame kind has not been stated.</exception>
    public FrameMap Build()
    {
        string[] missing = [.. new (string Name, bool Stated)[] { (nameof(Message), message is not null), (nameof(Retrieval), retrieval is not null), (nameof(ReadReceipt), readReceipt is not null), (nameof(ReceiveReceipt), receiveReceipt is not null) }.Where(kind => !kind.Stated).Select(kind => kind.Name)];
        if (missing.Length > 0) { throw new InvalidOperationException($"The frame mapping for {typeof(TFrame).Name} does not state: {string.Join(", ", missing)}"); }

        return new FrameMap
        {
            Type = typeof(TFrame),
            Serializer = serializer,
            Create = () => create(),
            Message = message!,
            Retrieval = retrieval!,
            ReadReceipt = readReceipt!,
            ReceiveReceipt = receiveReceipt!,
            Heartbeat = heartbeat
        };
    }
}
