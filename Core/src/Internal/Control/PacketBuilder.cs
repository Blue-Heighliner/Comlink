namespace BlueHeighliner.Comlink.Control;

/// <summary>Implements <see cref="IPacketBuilder{TPacket}"/>, collecting the mappings and turning them into a <see cref="PacketMap"/>.</summary>
internal sealed class PacketBuilder<TPacket> : IPacketBuilder<TPacket> where TPacket : class, new()
{
    private readonly Dictionary<string, (Delegate Get, Delegate Set)> fields = [];
    private INetworkSerializer serializer = new ProtobufNetworkSerializer(typeof(TPacket));
    private Func<TPacket> create = () => new();
    private int size = 16 * 1024;
    private int window = 1;

    /// <summary>The initial packet processor, if stated.</summary>
    public ProcessorRegistration<IInitialProcessor>? Initial { get; private set; }

    /// <inheritdoc />
    public IPacketBuilder<TPacket> PayloadId(Func<TPacket, int> get, Action<TPacket, int> set) => Map(nameof(PayloadId), get, set);

    /// <inheritdoc />
    public IPacketBuilder<TPacket> PayloadId(Expression<Func<TPacket, int>> property) => Map(nameof(PayloadId), property);

    /// <inheritdoc />
    public IPacketBuilder<TPacket> Index(Func<TPacket, int> get, Action<TPacket, int> set) => Map(nameof(Index), get, set);

    /// <inheritdoc />
    public IPacketBuilder<TPacket> Index(Expression<Func<TPacket, int>> property) => Map(nameof(Index), property);

    /// <inheritdoc />
    public IPacketBuilder<TPacket> Count(Func<TPacket, int> get, Action<TPacket, int> set) => Map(nameof(Count), get, set);

    /// <inheritdoc />
    public IPacketBuilder<TPacket> Count(Expression<Func<TPacket, int>> property) => Map(nameof(Count), property);

    /// <inheritdoc />
    public IPacketBuilder<TPacket> PayloadLength(Func<TPacket, int> get, Action<TPacket, int> set) => Map(nameof(PayloadLength), get, set);

    /// <inheritdoc />
    public IPacketBuilder<TPacket> PayloadLength(Expression<Func<TPacket, int>> property) => Map(nameof(PayloadLength), property);

    /// <inheritdoc />
    public IPacketBuilder<TPacket> Data(Func<TPacket, ReadOnlyMemory<byte>> get, Action<TPacket, ReadOnlyMemory<byte>> set) => Map(nameof(Data), get, set);

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
    public IPacketBuilder<TPacket> Serializer(INetworkSerializer serializer)
    {
        this.serializer = serializer;
        return this;
    }

    /// <inheritdoc />
    public IPacketBuilder<TPacket> Create(Func<TPacket> create)
    {
        this.create = create;
        return this;
    }

    /// <inheritdoc />
    public IPacketBuilder<TPacket> InitialProcessor<TProcessor>() where TProcessor : IInitialPacketProcessor<TPacket>
    {
        Initial = new ProcessorRegistration<IInitialProcessor>(typeof(TProcessor), processor => new InitialPacketProcessorAdapter<TPacket>((IInitialPacketProcessor<TPacket>)processor));
        return this;
    }

    /// <summary>Builds the engine-side map.</summary>
    /// <exception cref="InvalidOperationException">A packet field has not been mapped.</exception>
    public PacketMap Build()
    {
        string[] missing = [.. new[] { nameof(PayloadId), nameof(Index), nameof(Count), nameof(PayloadLength), nameof(Data) }.Where(name => !fields.ContainsKey(name))];
        if (missing.Length > 0) { throw new InvalidOperationException($"The packet mapping for {typeof(TPacket).Name} does not map: {string.Join(", ", missing)}"); }

        Func<object, T> Getter<T>(string name) => packet => ((Func<TPacket, T>)fields[name].Get)((TPacket)packet);
        Action<object, T> Setter<T>(string name) => (packet, value) => ((Action<TPacket, T>)fields[name].Set)((TPacket)packet, value);

        return new PacketMap
        {
            Type = typeof(TPacket),
            Serializer = serializer,
            Size = size,
            Window = window,
            Create = () => create(),
            GetPayloadId = Getter<int>(nameof(PayloadId)),
            SetPayloadId = Setter<int>(nameof(PayloadId)),
            GetIndex = Getter<int>(nameof(Index)),
            SetIndex = Setter<int>(nameof(Index)),
            GetCount = Getter<int>(nameof(Count)),
            SetCount = Setter<int>(nameof(Count)),
            GetPayloadLength = Getter<int>(nameof(PayloadLength)),
            SetPayloadLength = Setter<int>(nameof(PayloadLength)),
            GetData = Getter<ReadOnlyMemory<byte>>(nameof(Data)),
            SetData = Setter<ReadOnlyMemory<byte>>(nameof(Data))
        };
    }

    private PacketBuilder<TPacket> Map<T>(string name, Expression<Func<TPacket, T>> property)
    {
        (Func<TPacket, T> get, Action<TPacket, T> set) = PropertyAccessor.Create(property);
        return Map(name, get, set);
    }

    private PacketBuilder<TPacket> Map<T>(string name, Func<TPacket, T> get, Action<TPacket, T> set)
    {
        fields[name] = (get, set);
        return this;
    }
}
