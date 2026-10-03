namespace BlueHeighliner.Comlink.Control;

/// <summary>Implements <see cref="IFrameBuilder{TFrame}"/>, collecting the mappings and turning them into a <see cref="FrameMap"/>.</summary>
internal sealed class FrameBuilder<TFrame> : IFrameBuilder<TFrame> where TFrame : class, new()
{
    private readonly Dictionary<string, (Delegate Get, Delegate Set)> fields = [];
    private ServiceRegistration<IFrameSerializer> serializer = new(_ => new ProtobufSerializer(typeof(TFrame)));
    private readonly List<ServiceRegistration<AutoForwardControllerDefinition>> autoForwardControllers = [];
    private Func<TFrame> create = () => new();
    private ServiceRegistration<IMessageFrameHandler>? message;
    private ServiceRegistration<IRetrievalFrameHandler>? retrieval;
    private ServiceRegistration<IReceiptFrameHandler>? readReceipt;
    private ServiceRegistration<IReceiptFrameHandler>? receiveReceipt;

    /// <summary>The initial message processor, if stated.</summary>
    public ServiceRegistration<IInitialProcessor>? Initial { get; private set; }

    /// <summary>The processor that reacts to peer activity, if stated.</summary>
    public ServiceRegistration<INetworkHandler>? NetworkHandler { get; private set; }

    /// <summary>The custom auto forward controllers.</summary>
    public IReadOnlyList<ServiceRegistration<AutoForwardControllerDefinition>> AutoForwardControllers => autoForwardControllers;

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Id(Func<TFrame, string> get, Action<TFrame, string> set) => Map(nameof(Id), get, set);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Id(Expression<Func<TFrame, string>> property) => Map(nameof(Id), property);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Sender(Func<TFrame, string> get, Action<TFrame, string> set) => Map(nameof(Sender), get, set);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Sender(Expression<Func<TFrame, string>> property) => Map(nameof(Sender), property);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Addresses(Func<TFrame, IEnumerable<(string Name, AddressType Type, string Information)>> get, Action<TFrame, IReadOnlyList<(string Name, AddressType Type, string Information)>> set)
        => Map<List<MessageAddress>>(
            nameof(Addresses),
            message => [.. get(message).Select(address => new MessageAddress { UserName = address.Name, Type = address.Type, Information = address.Information })],
            (message, addresses) => set(message, [.. addresses.Select(address => (address.UserName, address.Type, address.Information))]));

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Addresses(Func<TFrame, IEnumerable<(string Name, AddressType Type)>> get, Action<TFrame, IReadOnlyList<(string Name, AddressType Type)>> set)
        => Addresses(
            message => get(message).Select(address => (address.Name, address.Type, string.Empty)),
            (message, addresses) => set(message, [.. addresses.Select(address => (address.Name, address.Type))]));

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Message<THandler>() where THandler : IMessageHandler<TFrame>
    {
        message = ServiceRegistration<IMessageFrameHandler>.Of(typeof(THandler), handler => new MessageFrameHandler<TFrame>((IMessageHandler<TFrame>)handler));
        return this;
    }

    /// <inheritdoc />
    public IFrameBuilder<TFrame> AutoForward<TController>() where TController : IAutoForwardController<TFrame>
    {
        autoForwardControllers.Add(ServiceRegistration<AutoForwardControllerDefinition>.Of(typeof(TController), instance =>
        {
            IAutoForwardController<TFrame> controller = (IAutoForwardController<TFrame>)instance;
            return new AutoForwardControllerDefinition { Name = controller.Name, Users = [.. controller.Users], Filter = frame => controller.Accepts((TFrame)frame) };
        }));
        return this;
    }

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Retrieval<THandler>() where THandler : IRetrievalHandler<TFrame>
    {
        retrieval = ServiceRegistration<IRetrievalFrameHandler>.Of(typeof(THandler), handler => new RetrievalFrameHandler<TFrame>((IRetrievalHandler<TFrame>)handler));
        return this;
    }

    /// <inheritdoc />
    public IFrameBuilder<TFrame> ReadReceipt<THandler>() where THandler : IReadReceiptHandler<TFrame>
    {
        readReceipt = ServiceRegistration<IReceiptFrameHandler>.Of(typeof(THandler), instance =>
        {
            IReadReceiptHandler<TFrame> handler = (IReadReceiptHandler<TFrame>)instance;
            return new ReceiptFrameHandler<TFrame>(handler.Priority, handler.IsValid, handler.Create, handler.GetMessageId);
        });
        return this;
    }

    /// <inheritdoc />
    public IFrameBuilder<TFrame> ReceiveReceipt<THandler>() where THandler : IReceiveReceiptHandler<TFrame>
    {
        receiveReceipt = ServiceRegistration<IReceiptFrameHandler>.Of(typeof(THandler), instance =>
        {
            IReceiveReceiptHandler<TFrame> handler = (IReceiveReceiptHandler<TFrame>)instance;
            return new ReceiptFrameHandler<TFrame>(handler.Priority, handler.IsValid, handler.Create, handler.GetMessageId);
        });
        return this;
    }

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Serializer<TSerializer>() where TSerializer : IFrameSerializer
    {
        serializer = ServiceRegistration<IFrameSerializer>.Of(typeof(TSerializer), instance => (IFrameSerializer)instance);
        return this;
    }

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Create(Func<TFrame> create)
    {
        this.create = create;
        return this;
    }

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Processor<TProcessor>() where TProcessor : INetworkProcessor<TFrame>
    {
        NetworkHandler = ServiceRegistration<INetworkHandler>.Of(typeof(TProcessor), processor => new NetworkProcessorAdapter<TFrame>((INetworkProcessor<TFrame>)processor));
        return this;
    }

    /// <inheritdoc />
    public IFrameBuilder<TFrame> InitialProcessor<TProcessor>() where TProcessor : IInitialFrameProcessor<TFrame>
    {
        Initial = ServiceRegistration<IInitialProcessor>.Of(typeof(TProcessor), processor => new InitialFrameProcessorAdapter<TFrame>((IInitialFrameProcessor<TFrame>)processor));
        return this;
    }

    /// <summary>Builds the engine-side map.</summary>
    /// <exception cref="InvalidOperationException">A logical field or frame kind has not been stated.</exception>
    public FrameMap Build()
    {
        string[] missing = [.. new[] { nameof(Id), nameof(Sender), nameof(Addresses) }.Where(name => !fields.ContainsKey(name)),
            .. new (string Name, bool Stated)[] { (nameof(Message), message is not null), (nameof(Retrieval), retrieval is not null), (nameof(ReadReceipt), readReceipt is not null), (nameof(ReceiveReceipt), receiveReceipt is not null) }.Where(kind => !kind.Stated).Select(kind => kind.Name)];
        if (missing.Length > 0) { throw new InvalidOperationException($"The frame mapping for {typeof(TFrame).Name} does not state: {string.Join(", ", missing)}"); }

        Func<object, T> Getter<T>(string name) => frame => ((Func<TFrame, T>)fields[name].Get)((TFrame)frame);
        Action<object, T> Setter<T>(string name) => (frame, value) => ((Action<TFrame, T>)fields[name].Set)((TFrame)frame, value);

        return new FrameMap
        {
            Type = typeof(TFrame),
            Serializer = serializer,
            Create = () => create(),
            GetId = Getter<string>(nameof(Id)),
            SetId = Setter<string>(nameof(Id)),
            GetSender = Getter<string>(nameof(Sender)),
            SetSender = Setter<string>(nameof(Sender)),
            GetAddresses = Getter<List<MessageAddress>>(nameof(Addresses)),
            SetAddresses = Setter<List<MessageAddress>>(nameof(Addresses)),
            Message = message!,
            Retrieval = retrieval!,
            ReadReceipt = readReceipt!,
            ReceiveReceipt = receiveReceipt!
        };
    }

    private FrameBuilder<TFrame> Map<T>(string name, Expression<Func<TFrame, T>> property)
    {
        (Func<TFrame, T> get, Action<TFrame, T> set) = PropertyAccessor.Create(property);
        return Map(name, get, set);
    }

    private FrameBuilder<TFrame> Map<T>(string name, Func<TFrame, T> get, Action<TFrame, T> set)
    {
        fields[name] = (get, set);
        return this;
    }
}
