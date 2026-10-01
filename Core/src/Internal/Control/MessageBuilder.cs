namespace BlueHeighliner.Comlink.Control;

/// <summary>Implements <see cref="IMessageBuilder{TMessage}"/>, collecting the mappings and turning them into a <see cref="MessageMap"/>.</summary>
internal sealed class MessageBuilder<TMessage> : IMessageBuilder<TMessage> where TMessage : class, new()
{
    private readonly Dictionary<string, (Delegate Get, Delegate Set)> fields = [];
    private ServiceRegistration<INetworkSerializer> serializer = new(_ => new ProtobufNetworkSerializer(typeof(TMessage)));
    private readonly List<AutoForwardControllerDefinition> autoForwardControllers = [];
    private Func<TMessage> create = () => new();

    /// <summary>How many copies of a received message print, if stated.</summary>
    public Func<object, int>? PrintCountValue { get; private set; }

    /// <summary>The initial message processor, if stated.</summary>
    public ServiceRegistration<IInitialProcessor>? Initial { get; private set; }

    /// <summary>The processor that reacts to peer activity, if stated.</summary>
    public ServiceRegistration<INetworkHandler>? NetworkHandler { get; private set; }

    /// <summary>The custom auto forward controllers.</summary>
    public IReadOnlyList<AutoForwardControllerDefinition> AutoForwardControllers => autoForwardControllers;

    /// <inheritdoc />
    public IMessageBuilder<TMessage> Id(Func<TMessage, string> get, Action<TMessage, string> set) => Map(nameof(Id), get, set);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> Id(Expression<Func<TMessage, string>> property) => Map(nameof(Id), property);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> Sender(Func<TMessage, string> get, Action<TMessage, string> set) => Map(nameof(Sender), get, set);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> Sender(Expression<Func<TMessage, string>> property) => Map(nameof(Sender), property);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> Subject(Func<TMessage, string> get, Action<TMessage, string> set) => Map(nameof(Subject), get, set);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> Subject(Expression<Func<TMessage, string>> property) => Map(nameof(Subject), property);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> Body(Func<TMessage, string> get, Action<TMessage, string> set) => Map(nameof(Body), get, set);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> Body(Expression<Func<TMessage, string>> property) => Map(nameof(Body), property);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> Addresses(Func<TMessage, IEnumerable<(string Name, AddressType Type, string Information)>> get, Action<TMessage, IReadOnlyList<(string Name, AddressType Type, string Information)>> set)
        => Map<List<MessageAddress>>(
            nameof(Addresses),
            message => [.. get(message).Select(address => new MessageAddress { UserName = address.Name, Type = address.Type, Information = address.Information })],
            (message, addresses) => set(message, [.. addresses.Select(address => (address.UserName, address.Type, address.Information))]));

    /// <inheritdoc />
    public IMessageBuilder<TMessage> Addresses(Func<TMessage, IEnumerable<(string Name, AddressType Type)>> get, Action<TMessage, IReadOnlyList<(string Name, AddressType Type)>> set)
        => Addresses(
            message => get(message).Select(address => (address.Name, address.Type, string.Empty)),
            (message, addresses) => set(message, [.. addresses.Select(address => (address.Name, address.Type))]));

    /// <inheritdoc />
    public IMessageBuilder<TMessage> SentAt(Func<TMessage, DateTime> get, Action<TMessage, DateTime> set) => Map(nameof(SentAt), get, set);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> SentAt(Expression<Func<TMessage, DateTime>> property) => Map(nameof(SentAt), property);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> ConfirmationId(Func<TMessage, string> get, Action<TMessage, string> set) => Map(nameof(ConfirmationId), get, set);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> ConfirmationId(Expression<Func<TMessage, string>> property) => Map(nameof(ConfirmationId), property);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> Retrieval(Action<IRetrievalBuilder<TMessage>> map)
    {
        map(new RetrievalBuilder<TMessage>(fields));
        return this;
    }

    /// <inheritdoc />
    public IMessageBuilder<TMessage> IsAlert(Func<TMessage, bool> get, Action<TMessage, bool> set) => Map(nameof(IsAlert), get, set);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> IsAlert(Expression<Func<TMessage, bool>> property) => Map(nameof(IsAlert), property);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> Priority(Func<TMessage, int> get, Action<TMessage, int> set) => Map(nameof(Priority), get, set);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> Priority(Expression<Func<TMessage, int>> property) => Map(nameof(Priority), property);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> Tag(Func<TMessage, string> get, Action<TMessage, string> set) => Map(nameof(Tag), get, set);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> Tag(Expression<Func<TMessage, string>> property) => Map(nameof(Tag), property);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> SecurityLevel(Func<TMessage, string> get, Action<TMessage, string> set) => Map(nameof(SecurityLevel), get, set);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> SecurityLevel(Expression<Func<TMessage, string>> property) => Map(nameof(SecurityLevel), property);

    /// <inheritdoc />
    public IMessageBuilder<TMessage> Serializer<TSerializer>() where TSerializer : INetworkSerializer
    {
        serializer = ServiceRegistration<INetworkSerializer>.Of(typeof(TSerializer), instance => (INetworkSerializer)instance);
        return this;
    }

    /// <inheritdoc />
    public IMessageBuilder<TMessage> Create(Func<TMessage> create)
    {
        this.create = create;
        return this;
    }

    /// <inheritdoc />
    public IMessageBuilder<TMessage> PrintCount(Func<TMessage, int> copies)
    {
        PrintCountValue = message => copies((TMessage)message);
        return this;
    }

    /// <inheritdoc />
    public IMessageBuilder<TMessage> AutoForward(string name, IEnumerable<string> users, Func<TMessage, bool> filter)
    {
        AutoForwardControllerDefinition definition = new() { Name = name, Users = [.. users], Filter = message => filter((TMessage)message) };
        int existingIndex = autoForwardControllers.FindIndex(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0) { autoForwardControllers[existingIndex] = definition; }
        else { autoForwardControllers.Add(definition); }
        return this;
    }

    /// <inheritdoc />
    public IMessageBuilder<TMessage> Processor<TProcessor>() where TProcessor : INetworkProcessor<TMessage>
    {
        NetworkHandler = ServiceRegistration<INetworkHandler>.Of(typeof(TProcessor), processor => new NetworkProcessorAdapter<TMessage>((INetworkProcessor<TMessage>)processor));
        return this;
    }

    /// <inheritdoc />
    public IMessageBuilder<TMessage> InitialProcessor<TProcessor>() where TProcessor : IInitialMessageProcessor<TMessage>
    {
        Initial = ServiceRegistration<IInitialProcessor>.Of(typeof(TProcessor), processor => new InitialMessageProcessorAdapter<TMessage>((IInitialMessageProcessor<TMessage>)processor));
        return this;
    }

    /// <summary>Builds the engine-side map.</summary>
    /// <exception cref="InvalidOperationException">A logical field has not been mapped.</exception>
    public MessageMap Build()
    {
        string[] missing = [.. new[] { nameof(Id), nameof(Sender), nameof(Subject), nameof(Body), nameof(Addresses), nameof(SentAt), nameof(ConfirmationId), nameof(IsAlert), nameof(Priority), nameof(Tag), nameof(SecurityLevel) }.Concat(RetrievalBuilder<TMessage>.Names).Where(name => !fields.ContainsKey(name))];
        if (missing.Length > 0) { throw new InvalidOperationException($"The message mapping for {typeof(TMessage).Name} does not map: {string.Join(", ", missing)}"); }

        Func<object, T> Getter<T>(string name) => message => ((Func<TMessage, T>)fields[name].Get)((TMessage)message);
        Action<object, T> Setter<T>(string name) => (message, value) => ((Action<TMessage, T>)fields[name].Set)((TMessage)message, value);

        return new MessageMap
        {
            Type = typeof(TMessage),
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
            GetIsRetrieval = Getter<bool>(RetrievalBuilder<TMessage>.IsRequestName),
            SetIsRetrieval = Setter<bool>(RetrievalBuilder<TMessage>.IsRequestName),
            GetRetrievalFrom = Getter<DateTime?>(RetrievalBuilder<TMessage>.FromName),
            SetRetrievalFrom = Setter<DateTime?>(RetrievalBuilder<TMessage>.FromName),
            GetRetrievalTo = Getter<DateTime?>(RetrievalBuilder<TMessage>.ToName),
            SetRetrievalTo = Setter<DateTime?>(RetrievalBuilder<TMessage>.ToName),
            GetRetrievalAuthors = Getter<List<string>>(RetrievalBuilder<TMessage>.AuthorsName),
            SetRetrievalAuthors = Setter<List<string>>(RetrievalBuilder<TMessage>.AuthorsName),
            GetRetrievalDestinations = Getter<List<string>>(RetrievalBuilder<TMessage>.DestinationsName),
            SetRetrievalDestinations = Setter<List<string>>(RetrievalBuilder<TMessage>.DestinationsName),
            GetRetrievalIds = Getter<List<string>>(RetrievalBuilder<TMessage>.IdsName),
            SetRetrievalIds = Setter<List<string>>(RetrievalBuilder<TMessage>.IdsName),
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

    private MessageBuilder<TMessage> Map<T>(string name, Expression<Func<TMessage, T>> property)
    {
        (Func<TMessage, T> get, Action<TMessage, T> set) = PropertyAccessor.Create(property);
        return Map(name, get, set);
    }

    private MessageBuilder<TMessage> Map<T>(string name, Func<TMessage, T> get, Action<TMessage, T> set)
    {
        fields[name] = (get, set);
        return this;
    }
}
