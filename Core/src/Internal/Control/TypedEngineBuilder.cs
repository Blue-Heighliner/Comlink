namespace BlueHeighliner.Comlink;

/// <summary>Implements <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}"/> and every sub-configuration chained off it (frames, packets, priorities and message levels), recording what the host states in the <see cref="EngineBuilder"/> that created it. They are one object, so a sub-configuration continues straight into the settings of the engine builder.</summary>
internal sealed class EngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>, IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>, IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>, IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>, IMessageLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>, IMessageAspectBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>, IAddressTypeBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>, IConnectionsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>, IExportsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>, IImportsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    private readonly EngineBuilder state;
    private readonly PriorityBuilder<TPriority> priorities = new();
    private readonly MessageLevelBuilder<TLevel> levels = new();
    private readonly MessageAspectBuilder<TAspect> aspects = new();
    private AddressType currentAddressType;
    private FrameBuilder<TFrame, TPriority, TLevel, TAspect>? frames;
    private PacketBuilder<TPacket, TPriority>? packets;

    /// <summary>Creates the builder over <paramref name="state"/>, which completes it when the configuration has finished.</summary>
    /// <param name="state">The state the host's statements are recorded in.</param>
    public EngineBuilder(EngineBuilder state)
    {
        this.state = state;
        state.Completions.Add(Complete);
    }

    /// <inheritdoc />
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Frames()
    {
        frames ??= new(state);
        return this;
    }

    /// <inheritdoc />
    public IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Packets()
    {
        if (typeof(TPacket) == typeof(NoPacket)) { throw new InvalidOperationException("Packets cannot be stated for a configuration whose types state no packet type."); }

        packets ??= new();
        return this;
    }

    /// <inheritdoc />
    public IMessageLevelsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> MessageLevels() => this;

    /// <inheritdoc />
    public IMessageAspectsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> MessageAspects() => this;

    /// <inheritdoc />
    public IPriorityBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Priorities() => this;

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Display<THandler>() where THandler : IDisplayHandler
    {
        state.DisplayHandler = ServiceRegistration<IDisplayHandler>.Of(typeof(THandler), instance => (IDisplayHandler)instance);
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Alarms<THandler>() where THandler : IAlarmHandler
    {
        state.AlarmHandler = ServiceRegistration<IAlarmHandler>.Of(typeof(THandler), instance => (IAlarmHandler)instance);
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Drafts<THandler>() where THandler : IDraftHandler<TPriority, TLevel, TAspect>
    {
        state.DraftHandler = ServiceRegistration<IDraftFrameHandler>.Of(typeof(THandler), instance => new DraftFrameHandler<TPriority, TLevel, TAspect>((IDraftHandler<TPriority, TLevel, TAspect>)instance, state.MessageLevelValues, state.MessageAspectValues));
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Logs<THandler>() where THandler : ILogHandler
    {
        state.LogHandler = ServiceRegistration<ILogHandler>.Of(typeof(THandler), instance => (ILogHandler)instance);
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Prints<THandler>() where THandler : IPrintHandler<TFrame>
    {
        state.PrintHandler = ServiceRegistration<IPrintFrameHandler>.Of(typeof(THandler), instance => new PrintFrameHandler<TFrame>((IPrintHandler<TFrame>)instance));
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Deletes<THandler>() where THandler : IDeleteHandler
    {
        state.DeleteHandler = ServiceRegistration<IDeleteHandler>.Of(typeof(THandler), instance => (IDeleteHandler)instance);
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> CommandLineOverrides(bool allowed)
    {
        state.AreCommandLineOverridesAllowed = allowed;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> ExternalSystem(IExternalSystem system)
    {
        if (!state.ExternalSystems.Contains(system)) { state.ExternalSystems.Add(system); }
        return this;
    }

    /// <inheritdoc />
    public IExportsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Exports() => this;

    /// <inheritdoc />
    public IImportsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Imports() => this;

    /// <inheritdoc />
    IExportsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> IExportsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>.Format<TFormat>()
    {
        state.ExportFormats.Add(ServiceRegistration<IExportFormat>.Of(typeof(TFormat), instance => (IExportFormat)instance));
        return this;
    }

    /// <inheritdoc />
    IImportsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> IImportsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>.Format<TFormat>()
    {
        state.ImportFormats.Add(ServiceRegistration<IImportFormat>.Of(typeof(TFormat), instance => (IImportFormat)instance));
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Message{THandler}"/>
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Message<THandler>() where THandler : IMessageHandler<TFrame, TPriority, TLevel, TAspect>
    {
        RequireFrames().Message<THandler>();
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.AutoForward{TController}"/>
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> AutoForward<TController>() where TController : IAutoForwardController<TFrame>
    {
        RequireFrames().AutoForward<TController>();
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Retrieval{THandler}"/>
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Retrieval<THandler>() where THandler : IRetrievalHandler<TFrame, TPriority>
    {
        RequireFrames().Retrieval<THandler>();
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.ReadReceipt{THandler}"/>
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> ReadReceipt<THandler>() where THandler : IReadReceiptHandler<TFrame, TPriority>
    {
        RequireFrames().ReadReceipt<THandler>();
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.ReceiveReceipt{THandler}"/>
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> ReceiveReceipt<THandler>() where THandler : IReceiveReceiptHandler<TFrame, TPriority>
    {
        RequireFrames().ReceiveReceipt<THandler>();
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Create"/>
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Create(Func<TFrame> create)
    {
        RequireFrames().Create(create);
        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Processor{TProcessor}"/>
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Processor<TProcessor>() where TProcessor : INetworkProcessor<TFrame>
    {
        RequireFrames().Processor<TProcessor>();
        return this;
    }

    /// <inheritdoc />
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>.Heartbeat<THandler>()
    {
        RequireFrames().Heartbeat<THandler>();
        return this;
    }

    /// <inheritdoc />
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>.Serializer<TSerializer>()
    {
        RequireFrames().Serializer<TSerializer>();
        return this;
    }

    /// <inheritdoc />
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>.InitialProcessor<TProcessor>()
    {
        RequireFrames().InitialProcessor<TProcessor>();
        return this;
    }

    /// <inheritdoc cref="IPacketBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Frame{THandler}"/>
    public IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Frame<THandler>() where THandler : IFramePacketHandler<TPacket>
    {
        RequirePackets().Frame<THandler>();
        return this;
    }

    /// <inheritdoc />
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>.Heartbeat<THandler>()
    {
        RequirePackets().Heartbeat<THandler>();
        return this;
    }

    /// <inheritdoc />
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>.Serializer<TSerializer>()
    {
        RequirePackets().Serializer<TSerializer>();
        return this;
    }

    /// <inheritdoc />
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>.InitialProcessor<TProcessor>()
    {
        RequirePackets().InitialProcessor<TProcessor>();
        return this;
    }

    /// <inheritdoc />
    public IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Size(int bytes)
    {
        state.PacketSizeValue = bytes;
        return this;
    }

    /// <inheritdoc />
    public IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Window(int packets)
    {
        state.PacketWindowValue = packets;
        return this;
    }

    /// <inheritdoc />
    public IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Priority(TPriority priority)
    {
        priorities.Priority(priority);
        return this;
    }

    /// <inheritdoc />
    public IPriorityBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Block(TPriority? priority, string? tag)
    {
        priorities.Block(priority, tag);
        return this;
    }

    /// <inheritdoc />
    public IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Mode(PriorityMode mode)
    {
        priorities.Mode(mode);
        return this;
    }

    /// <inheritdoc />
    IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>.Label(string label)
    {
        priorities.Label(label);
        return this;
    }

    /// <inheritdoc />
    public IConnectionsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Connections() => this;

    /// <inheritdoc />
    public IConnectionsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Msmt(MsmtConnectionOptions options)
    {
        state.MsmtOptionsValue = options;
        return this;
    }

    /// <inheritdoc />
    public IConnectionsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Hdlc(HdlcPeerOptions options)
    {
        state.HdlcOptionsValue = options;
        return this;
    }

    /// <inheritdoc />
    public IAddressTypesBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> AddressTypes() => this;

    /// <inheritdoc />
    public IAddressTypeBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Type(AddressType type)
    {
        currentAddressType = type;
        return this;
    }

    /// <inheritdoc />
    IAddressTypeBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> IAddressTypeBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>.Label(string label)
    {
        state.AddressTypeLabels[currentAddressType] = label;
        return this;
    }

    /// <inheritdoc />
    public IMessageAspectBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Aspect(TAspect aspect)
    {
        aspects.Aspect(aspect);
        return this;
    }

    /// <inheritdoc />
    IMessageAspectBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> IMessageAspectBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>.Label(string label)
    {
        aspects.Label(label);
        return this;
    }

    /// <inheritdoc />
    public IMessageLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Level(TLevel level)
    {
        levels.Level(level);
        return this;
    }

    /// <inheritdoc />
    public IMessageLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Color(string color)
    {
        levels.Color(color);
        return this;
    }

    /// <inheritdoc />
    IMessageLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> IMessageLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>.Label(string label)
    {
        levels.Label(label);
        return this;
    }

    private FrameBuilder<TFrame, TPriority, TLevel, TAspect> RequireFrames() => frames ?? throw new InvalidOperationException("Frames() must be called before the frame handlers are stated.");

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

        List<MessageLevel> builtLevels = levels.Build();
        if (builtLevels.Count > 0)
        {
            state.MessageLevelValues.Clear();
            state.MessageLevelValues.AddRange(builtLevels);
        }

        List<MessageAspect> builtAspects = aspects.Build();
        if (builtAspects.Count > 0)
        {
            state.MessageAspectValues.Clear();
            state.MessageAspectValues.AddRange(builtAspects);
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
