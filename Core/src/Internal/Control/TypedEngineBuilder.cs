namespace BlueHeighliner.Comlink;

/// <summary>Implements <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel}"/> and every sub-configuration chained off it (frames, packets, priorities and security levels), recording what the host states in the <see cref="EngineBuilder"/> that created it. They are one object, so a sub-configuration continues straight into the settings of the engine builder.</summary>
internal sealed class EngineBuilder<TFrame, TPacket, TPriority, TLevel> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel>, IFrameBuilder<TFrame, TPacket, TPriority, TLevel>, IPacketBuilder<TFrame, TPacket, TPriority, TLevel>, IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel>, ISecurityLevelBuilder<TFrame, TPacket, TPriority, TLevel>, IAddressTypeBuilder<TFrame, TPacket, TPriority, TLevel>, IConnectionsBuilder<TFrame, TPacket, TPriority, TLevel>, IExportsBuilder<TFrame, TPacket, TPriority, TLevel>, IImportsBuilder<TFrame, TPacket, TPriority, TLevel> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum
{
    private readonly EngineBuilder state;
    private readonly PriorityBuilder<TPriority> priorities = new();
    private readonly SecurityLevelBuilder<TLevel> levels = new();
    private AddressType currentAddressType;
    private FrameBuilder<TFrame, TPriority, TLevel>? frames;
    private PacketBuilder<TPacket, TPriority>? packets;

    /// <summary>Creates the builder over <paramref name="state"/>, which completes it when the configuration has finished.</summary>
    /// <param name="state">The state the host's statements are recorded in.</param>
    public EngineBuilder(EngineBuilder state)
    {
        this.state = state;
        state.Completions.Add(Complete);
    }

    /// <inheritdoc />
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel> Frames()
    {
        frames ??= new(state);
        return this;
    }

    /// <inheritdoc />
    public IPacketBuilder<TFrame, TPacket, TPriority, TLevel> Packets()
    {
        if (typeof(TPacket) == typeof(NoPacket)) { throw new InvalidOperationException("Packets cannot be stated for a configuration whose types state no packet type."); }

        packets ??= new();
        return this;
    }

    /// <inheritdoc />
    public ISecurityLevelsBuilder<TFrame, TPacket, TPriority, TLevel> SecurityLevels() => this;

    /// <inheritdoc />
    public IPriorityBuilder<TFrame, TPacket, TPriority, TLevel> Priorities() => this;

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel> Display<THandler>() where THandler : IDisplayHandler
    {
        state.DisplayHandler = ServiceRegistration<IDisplayHandler>.Of(typeof(THandler), instance => (IDisplayHandler)instance);
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel> Alarms<THandler>() where THandler : IAlarmHandler
    {
        state.AlarmHandler = ServiceRegistration<IAlarmHandler>.Of(typeof(THandler), instance => (IAlarmHandler)instance);
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel> Drafts<THandler>() where THandler : IDraftHandler<TPriority, TLevel>
    {
        state.DraftHandler = ServiceRegistration<IDraftFrameHandler>.Of(typeof(THandler), instance => new DraftFrameHandler<TPriority, TLevel>((IDraftHandler<TPriority, TLevel>)instance, state.SecurityLevelValues));
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel> Logs<THandler>() where THandler : ILogHandler
    {
        state.LogHandler = ServiceRegistration<ILogHandler>.Of(typeof(THandler), instance => (ILogHandler)instance);
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel> Prints<THandler>() where THandler : IPrintHandler<TFrame>
    {
        state.PrintHandler = ServiceRegistration<IPrintFrameHandler>.Of(typeof(THandler), instance => new PrintFrameHandler<TFrame>((IPrintHandler<TFrame>)instance));
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel> Deletes<THandler>() where THandler : IDeleteHandler
    {
        state.DeleteHandler = ServiceRegistration<IDeleteHandler>.Of(typeof(THandler), instance => (IDeleteHandler)instance);
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel> CommandLineOverrides(bool allowed)
    {
        state.AreCommandLineOverridesAllowed = allowed;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel> ExternalSystem(IExternalSystem system)
    {
        if (!state.ExternalSystems.Contains(system)) { state.ExternalSystems.Add(system); }
        return this;
    }

    /// <inheritdoc />
    public IExportsBuilder<TFrame, TPacket, TPriority, TLevel> Exports() => this;

    /// <inheritdoc />
    public IImportsBuilder<TFrame, TPacket, TPriority, TLevel> Imports() => this;

    /// <inheritdoc />
    IExportsBuilder<TFrame, TPacket, TPriority, TLevel> IExportsBuilder<TFrame, TPacket, TPriority, TLevel>.Format<TFormat>()
    {
        state.ExportFormats.Add(ServiceRegistration<IExportFormat>.Of(typeof(TFormat), instance => (IExportFormat)instance));
        return this;
    }

    /// <inheritdoc />
    IImportsBuilder<TFrame, TPacket, TPriority, TLevel> IImportsBuilder<TFrame, TPacket, TPriority, TLevel>.Format<TFormat>()
    {
        state.ImportFormats.Add(ServiceRegistration<IImportFormat>.Of(typeof(TFormat), instance => (IImportFormat)instance));
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.Message{THandler}"/>
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel> Message<THandler>() where THandler : IMessageHandler<TFrame, TPriority, TLevel>
    {
        RequireFrames().Message<THandler>();
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.AutoForward{TController}"/>
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel> AutoForward<TController>() where TController : IAutoForwardController<TFrame>
    {
        RequireFrames().AutoForward<TController>();
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.Retrieval{THandler}"/>
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel> Retrieval<THandler>() where THandler : IRetrievalHandler<TFrame, TPriority>
    {
        RequireFrames().Retrieval<THandler>();
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.ReadReceipt{THandler}"/>
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel> ReadReceipt<THandler>() where THandler : IReadReceiptHandler<TFrame, TPriority>
    {
        RequireFrames().ReadReceipt<THandler>();
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.ReceiveReceipt{THandler}"/>
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel> ReceiveReceipt<THandler>() where THandler : IReceiveReceiptHandler<TFrame, TPriority>
    {
        RequireFrames().ReceiveReceipt<THandler>();
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.Create"/>
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel> Create(Func<TFrame> create)
    {
        RequireFrames().Create(create);
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.Processor{TProcessor}"/>
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel> Processor<TProcessor>() where TProcessor : INetworkProcessor<TFrame>
    {
        RequireFrames().Processor<TProcessor>();
        return this;
    }

    /// <inheritdoc />
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel> IFrameBuilder<TFrame, TPacket, TPriority, TLevel>.Heartbeat<THandler>()
    {
        RequireFrames().Heartbeat<THandler>();
        return this;
    }

    /// <inheritdoc />
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel> IFrameBuilder<TFrame, TPacket, TPriority, TLevel>.Serializer<TSerializer>()
    {
        RequireFrames().Serializer<TSerializer>();
        return this;
    }

    /// <inheritdoc />
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel> IFrameBuilder<TFrame, TPacket, TPriority, TLevel>.InitialProcessor<TProcessor>()
    {
        RequireFrames().InitialProcessor<TProcessor>();
        return this;
    }

    /// <inheritdoc cref="IPacketBuilder{TFrame, TPacket, TPriority, TLevel}.Frame{THandler}"/>
    public IPacketBuilder<TFrame, TPacket, TPriority, TLevel> Frame<THandler>() where THandler : IFramePacketHandler<TPacket>
    {
        RequirePackets().Frame<THandler>();
        return this;
    }

    /// <inheritdoc />
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel> IPacketBuilder<TFrame, TPacket, TPriority, TLevel>.Heartbeat<THandler>()
    {
        RequirePackets().Heartbeat<THandler>();
        return this;
    }

    /// <inheritdoc />
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel> IPacketBuilder<TFrame, TPacket, TPriority, TLevel>.Serializer<TSerializer>()
    {
        RequirePackets().Serializer<TSerializer>();
        return this;
    }

    /// <inheritdoc />
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel> IPacketBuilder<TFrame, TPacket, TPriority, TLevel>.InitialProcessor<TProcessor>()
    {
        RequirePackets().InitialProcessor<TProcessor>();
        return this;
    }

    /// <inheritdoc />
    public IPacketBuilder<TFrame, TPacket, TPriority, TLevel> Size(int bytes)
    {
        state.PacketSizeValue = bytes;
        return this;
    }

    /// <inheritdoc />
    public IPacketBuilder<TFrame, TPacket, TPriority, TLevel> Window(int packets)
    {
        state.PacketWindowValue = packets;
        return this;
    }

    /// <inheritdoc />
    public IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel> Priority(TPriority priority)
    {
        priorities.Priority(priority);
        return this;
    }

    /// <inheritdoc />
    public IPriorityBuilder<TFrame, TPacket, TPriority, TLevel> Block(TPriority? priority, string? tag)
    {
        priorities.Block(priority, tag);
        return this;
    }

    /// <inheritdoc />
    public IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel> Mode(PriorityMode mode)
    {
        priorities.Mode(mode);
        return this;
    }

    /// <inheritdoc />
    IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel> IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel>.Label(string label)
    {
        priorities.Label(label);
        return this;
    }

    /// <inheritdoc />
    public IConnectionsBuilder<TFrame, TPacket, TPriority, TLevel> Connections() => this;

    /// <inheritdoc />
    public IConnectionsBuilder<TFrame, TPacket, TPriority, TLevel> Msmt(MsmtConnectionOptions options)
    {
        state.MsmtOptionsValue = options;
        return this;
    }

    /// <inheritdoc />
    public IConnectionsBuilder<TFrame, TPacket, TPriority, TLevel> Hdlc(HdlcPeerOptions options)
    {
        state.HdlcOptionsValue = options;
        return this;
    }

    /// <inheritdoc />
    public IAddressTypesBuilder<TFrame, TPacket, TPriority, TLevel> AddressTypes() => this;

    /// <inheritdoc />
    public IAddressTypeBuilder<TFrame, TPacket, TPriority, TLevel> Type(AddressType type)
    {
        currentAddressType = type;
        return this;
    }

    /// <inheritdoc />
    IAddressTypeBuilder<TFrame, TPacket, TPriority, TLevel> IAddressTypeBuilder<TFrame, TPacket, TPriority, TLevel>.Label(string label)
    {
        state.AddressTypeLabels[currentAddressType] = label;
        return this;
    }

    /// <inheritdoc />
    public ISecurityLevelBuilder<TFrame, TPacket, TPriority, TLevel> Level(TLevel level)
    {
        levels.Level(level);
        return this;
    }

    /// <inheritdoc />
    public ISecurityLevelBuilder<TFrame, TPacket, TPriority, TLevel> Color(string color)
    {
        levels.Color(color);
        return this;
    }

    /// <inheritdoc />
    ISecurityLevelBuilder<TFrame, TPacket, TPriority, TLevel> ISecurityLevelBuilder<TFrame, TPacket, TPriority, TLevel>.Label(string label)
    {
        levels.Label(label);
        return this;
    }

    private FrameBuilder<TFrame, TPriority, TLevel> RequireFrames() => frames ?? throw new InvalidOperationException("Frames() must be called before the frame handlers are stated.");

    private PacketBuilder<TPacket, TPriority> RequirePackets() => packets ?? throw new InvalidOperationException("Packets() must be called before the packet handlers are stated.");

    private void Complete()
    {
        if (frames is not null)
        {
            state.FrameMap = frames.Build();
            state.InitialFrameProcessor = frames.Initial;
            state.NetworkHandler = frames.NetworkHandler;
            state.AutoForwardControllers.Clear();
            state.AutoForwardControllers.AddRange(frames.AutoForwardControllers);
        }

        if (packets is not null)
        {
            state.PacketMap = packets.Build();
            state.InitialPacketProcessor = packets.Initial;
        }

        List<SecurityLevel> builtLevels = levels.Build();
        if (builtLevels.Count > 0)
        {
            state.SecurityLevelValues.Clear();
            state.SecurityLevelValues.AddRange(builtLevels);
        }

        List<MessagePriorityOption> builtPriorities = priorities.Build();
        if (builtPriorities.Count > 0)
        {
            state.PriorityOptions.Clear();
            state.PriorityOptions.AddRange(builtPriorities);
        }

        state.BlockedCombinations.Clear();
        state.BlockedCombinations.AddRange(priorities.Blocks);
    }
}
