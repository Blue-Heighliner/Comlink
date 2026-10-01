namespace BlueHeighliner.Comlink.Control;

/// <summary>Implements <see cref="IFrameBuilder{TFrame}"/>, collecting the mappings and turning them into a <see cref="FrameMap"/>.</summary>
internal sealed class FrameBuilder<TFrame> : IFrameBuilder<TFrame> where TFrame : class, new()
{
    private readonly Dictionary<string, (Delegate Get, Delegate Set)> fields = [];
    private ServiceRegistration<INetworkSerializer> serializer = new(_ => new ProtobufNetworkSerializer(typeof(TFrame)));
    private readonly List<AutoForwardControllerDefinition> autoForwardControllers = [];
    private Func<TFrame> create = () => new();

    /// <summary>How many copies of a received message print, if stated.</summary>
    public Func<object, int>? PrintCountValue { get; private set; }

    /// <summary>The initial message processor, if stated.</summary>
    public ServiceRegistration<IInitialProcessor>? Initial { get; private set; }

    /// <summary>The processor that reacts to peer activity, if stated.</summary>
    public ServiceRegistration<INetworkHandler>? NetworkHandler { get; private set; }

    /// <summary>The custom auto forward controllers.</summary>
    public IReadOnlyList<AutoForwardControllerDefinition> AutoForwardControllers => autoForwardControllers;

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Id(Func<TFrame, string> get, Action<TFrame, string> set) => Map(nameof(Id), get, set);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Id(Expression<Func<TFrame, string>> property) => Map(nameof(Id), property);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Sender(Func<TFrame, string> get, Action<TFrame, string> set) => Map(nameof(Sender), get, set);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Sender(Expression<Func<TFrame, string>> property) => Map(nameof(Sender), property);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Subject(Func<TFrame, string> get, Action<TFrame, string> set) => Map(nameof(Subject), get, set);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Subject(Expression<Func<TFrame, string>> property) => Map(nameof(Subject), property);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Body(Func<TFrame, string> get, Action<TFrame, string> set) => Map(nameof(Body), get, set);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Body(Expression<Func<TFrame, string>> property) => Map(nameof(Body), property);

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
    public IFrameBuilder<TFrame> SentAt(Func<TFrame, DateTime> get, Action<TFrame, DateTime> set) => Map(nameof(SentAt), get, set);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> SentAt(Expression<Func<TFrame, DateTime>> property) => Map(nameof(SentAt), property);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> ConfirmationId(Func<TFrame, string> get, Action<TFrame, string> set) => Map(nameof(ConfirmationId), get, set);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> ConfirmationId(Expression<Func<TFrame, string>> property) => Map(nameof(ConfirmationId), property);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Retrieval(Action<IRetrievalBuilder<TFrame>> map)
    {
        map(new RetrievalBuilder<TFrame>(fields));
        return this;
    }

    /// <inheritdoc />
    public IFrameBuilder<TFrame> IsMessage(Func<TFrame, bool> get, Action<TFrame, bool> set) => Map(nameof(IsMessage), get, set);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> IsMessage(Expression<Func<TFrame, bool>> property) => Map(nameof(IsMessage), property);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> IsAlert(Func<TFrame, bool> get, Action<TFrame, bool> set) => Map(nameof(IsAlert), get, set);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> IsAlert(Expression<Func<TFrame, bool>> property) => Map(nameof(IsAlert), property);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Priority(Func<TFrame, int> get, Action<TFrame, int> set) => Map(nameof(Priority), get, set);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Priority(Expression<Func<TFrame, int>> property) => Map(nameof(Priority), property);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Tag(Func<TFrame, string> get, Action<TFrame, string> set) => Map(nameof(Tag), get, set);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Tag(Expression<Func<TFrame, string>> property) => Map(nameof(Tag), property);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> SecurityLevel(Func<TFrame, string> get, Action<TFrame, string> set) => Map(nameof(SecurityLevel), get, set);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> SecurityLevel(Expression<Func<TFrame, string>> property) => Map(nameof(SecurityLevel), property);

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Serializer<TSerializer>() where TSerializer : INetworkSerializer
    {
        serializer = ServiceRegistration<INetworkSerializer>.Of(typeof(TSerializer), instance => (INetworkSerializer)instance);
        return this;
    }

    /// <inheritdoc />
    public IFrameBuilder<TFrame> Create(Func<TFrame> create)
    {
        this.create = create;
        return this;
    }

    /// <inheritdoc />
    public IFrameBuilder<TFrame> PrintCount(Func<TFrame, int> copies)
    {
        PrintCountValue = message => copies((TFrame)message);
        return this;
    }

    /// <inheritdoc />
    public IFrameBuilder<TFrame> AutoForward(string name, IEnumerable<string> users, Func<TFrame, bool> filter)
    {
        AutoForwardControllerDefinition definition = new() { Name = name, Users = [.. users], Filter = message => filter((TFrame)message) };
        int existingIndex = autoForwardControllers.FindIndex(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0) { autoForwardControllers[existingIndex] = definition; }
        else { autoForwardControllers.Add(definition); }
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
    /// <exception cref="InvalidOperationException">A logical field has not been mapped.</exception>
    public FrameMap Build()
    {
        string[] missing = [.. new[] { nameof(Id), nameof(Sender), nameof(Subject), nameof(Body), nameof(Addresses), nameof(SentAt), nameof(ConfirmationId), nameof(IsMessage), nameof(IsAlert), nameof(Priority), nameof(Tag), nameof(SecurityLevel) }.Concat(RetrievalBuilder<TFrame>.Names).Where(name => !fields.ContainsKey(name))];
        if (missing.Length > 0) { throw new InvalidOperationException($"The frame mapping for {typeof(TFrame).Name} does not map: {string.Join(", ", missing)}"); }

        Func<object, T> Getter<T>(string name) => message => ((Func<TFrame, T>)fields[name].Get)((TFrame)message);
        Action<object, T> Setter<T>(string name) => (message, value) => ((Action<TFrame, T>)fields[name].Set)((TFrame)message, value);

        return new FrameMap
        {
            Type = typeof(TFrame),
            Serializer = serializer,
            Create = () => create(),
            GetId = Getter<string>(nameof(Id)),
            SetId = Setter<string>(nameof(Id)),
            GetSender = Getter<string>(nameof(Sender)),
            SetSender = Setter<string>(nameof(Sender)),
            GetSubject = Getter<string>(nameof(Subject)),
            SetSubject = Setter<string>(nameof(Subject)),
            GetBody = Getter<string>(nameof(Body)),
            SetBody = Setter<string>(nameof(Body)),
            GetAddresses = Getter<List<MessageAddress>>(nameof(Addresses)),
            SetAddresses = Setter<List<MessageAddress>>(nameof(Addresses)),
            GetSentAt = Getter<DateTime>(nameof(SentAt)),
            SetSentAt = Setter<DateTime>(nameof(SentAt)),
            GetConfirmationId = Getter<string>(nameof(ConfirmationId)),
            SetConfirmationId = Setter<string>(nameof(ConfirmationId)),
            GetIsRetrieval = Getter<bool>(RetrievalBuilder<TFrame>.IsRequestName),
            SetIsRetrieval = Setter<bool>(RetrievalBuilder<TFrame>.IsRequestName),
            GetRetrievalFrom = Getter<DateTime?>(RetrievalBuilder<TFrame>.FromName),
            SetRetrievalFrom = Setter<DateTime?>(RetrievalBuilder<TFrame>.FromName),
            GetRetrievalTo = Getter<DateTime?>(RetrievalBuilder<TFrame>.ToName),
            SetRetrievalTo = Setter<DateTime?>(RetrievalBuilder<TFrame>.ToName),
            GetRetrievalAuthors = Getter<List<string>>(RetrievalBuilder<TFrame>.AuthorsName),
            SetRetrievalAuthors = Setter<List<string>>(RetrievalBuilder<TFrame>.AuthorsName),
            GetRetrievalDestinations = Getter<List<string>>(RetrievalBuilder<TFrame>.DestinationsName),
            SetRetrievalDestinations = Setter<List<string>>(RetrievalBuilder<TFrame>.DestinationsName),
            GetRetrievalIds = Getter<List<string>>(RetrievalBuilder<TFrame>.IdsName),
            SetRetrievalIds = Setter<List<string>>(RetrievalBuilder<TFrame>.IdsName),
            GetIsMessage = Getter<bool>(nameof(IsMessage)),
            SetIsMessage = Setter<bool>(nameof(IsMessage)),
            GetIsAlert = Getter<bool>(nameof(IsAlert)),
            SetIsAlert = Setter<bool>(nameof(IsAlert)),
            GetPriority = Getter<int>(nameof(Priority)),
            SetPriority = Setter<int>(nameof(Priority)),
            GetTag = Getter<string>(nameof(Tag)),
            SetTag = Setter<string>(nameof(Tag)),
            GetSecurityLevel = Getter<string>(nameof(SecurityLevel)),
            SetSecurityLevel = Setter<string>(nameof(SecurityLevel))
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
