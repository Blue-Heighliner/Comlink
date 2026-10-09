namespace BlueHeighliner.Comlink;

/// <summary>Implements <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}"/> and every sub-configuration chained off it (frames, packets, priorities and message levels), recording what the host states in the <see cref="EngineBuilder"/> that created it. They are one object, so a sub-configuration continues straight into the settings of the engine builder.</summary>
internal sealed class EngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>, IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>, IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>, IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>, IMessageLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>, IMessageAspectBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>, IAddressTypeBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>, IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>, IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    private readonly EngineBuilder state;
    private readonly PriorityBuilder<TPriority> priorities = new();
    private readonly MessageLevelBuilder<TLevel> levels = new();
    private readonly MessageAspectBuilder<TAspect> aspects = new();
    private AddressType currentAddressType;
    private FrameBuilder<TFrame, TPriority, TLevel, TAspect>? frames;
    private PacketBuilder<TFrame, TPacket, TPriority>? packets;

    /// <summary>Creates the builder over <paramref name="state"/>, which completes it when the configuration has finished.</summary>
    /// <param name="state">The state the host's statements are recorded in.</param>
    public EngineBuilder(EngineBuilder state)
    {
        this.state = state;
        state.Completions.Add(Complete);
    }

    /// <inheritdoc />
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Frames<TProcessor>() where TProcessor : INetworkProcessor<TFrame, TPriority, TLevel, TAspect>
    {
        frames ??= new();
        frames.Processor<TProcessor>();
        return this;
    }

    /// <inheritdoc />
    public IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Packets<THandler>(int maxPayloadSize) where THandler : IPacketHandler<TFrame, TPacket>
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPayloadSize);
        if (typeof(TPacket) == typeof(NoPacket))
        {
            throw new InvalidOperationException("Packets cannot be stated for a configuration whose types state no packet type.");
        }

        packets ??= new();
        packets.Handler<THandler>();
        state.MaxPayloadSizeValue = maxPayloadSize;
        return this;
    }

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
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Prints<THandler>() where THandler : IPrintHandler<TPriority, TLevel, TAspect>
    {
        state.PrintHandler = ServiceRegistration<IPrintPolicy>.Of(typeof(THandler), instance => new PrintPolicy<TPriority, TLevel, TAspect>((IPrintHandler<TPriority, TLevel, TAspect>)instance));
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
        if (!state.ExternalSystems.Contains(system))
        {
            state.ExternalSystems.Add(system);
        }
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Export<TFormat>() where TFormat : IExportFormat
    {
        state.ExportFormats.Add(ServiceRegistration<IExportFormat>.Of(typeof(TFormat), instance => (IExportFormat)instance));
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Import<TFormat>() where TFormat : IImportFormat<TPriority, TLevel>
    {
        state.ImportFormats.Add(ServiceRegistration<IImportSource>.Of(typeof(TFormat), instance => new ImportSource<TPriority, TLevel>((IImportFormat<TPriority, TLevel>)instance)));
        return this;
    }

    /// <inheritdoc cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.AutoForwarder"/>
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> AutoForwarder(string name)
    {
        if (state.AutoForwarders.All(forwarder => forwarder.Name != name))
        {
            state.AutoForwarders.Add(new() { Name = name });
        }

        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Create"/>
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Create(Func<TFrame> create)
    {
        RequireFrames().Create(create);
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
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>.Handshake<TProcessor>()
    {
        RequireFrames().Handshake<TProcessor>();
        return this;
    }

    /// <inheritdoc />
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>.Handshake<TProcessor>()
    {
        RequirePackets().Handshake<TProcessor>();
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
    public IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Msmt() => this;

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Hdlc() => this;

    /// <inheritdoc />
    public IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> HandshakeTimeout(TimeSpan? handshakeTimeout)
    {
        state.MsmtOptionsValue = (state.MsmtOptionsValue ?? new()) with { HandshakeTimeout = handshakeTimeout };
        return this;
    }

    /// <inheritdoc />
    public IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> StallTimeout(TimeSpan? stallTimeout)
    {
        state.MsmtOptionsValue = (state.MsmtOptionsValue ?? new()) with { StallTimeout = stallTimeout };
        return this;
    }

    /// <inheritdoc />
    public IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> ResponseTimeout(TimeSpan? responseTimeout)
    {
        state.MsmtOptionsValue = (state.MsmtOptionsValue ?? new()) with { ResponseTimeout = responseTimeout };
        return this;
    }

    /// <inheritdoc />
    public IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> TcpKeepAliveTime(TimeSpan? tcpKeepAliveTime)
    {
        state.MsmtOptionsValue = (state.MsmtOptionsValue ?? new()) with { TcpKeepAliveTime = tcpKeepAliveTime };
        return this;
    }

    /// <inheritdoc />
    public IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> MaximumSessionLifetime(TimeSpan maximumSessionLifetime)
    {
        state.MsmtOptionsValue = (state.MsmtOptionsValue ?? new()) with { MaximumSessionLifetime = maximumSessionLifetime };
        return this;
    }

    /// <inheritdoc />
    public IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> SessionLifetime(TimeSpan sessionLifetime)
    {
        state.MsmtOptionsValue = (state.MsmtOptionsValue ?? new()) with { SessionLifetime = sessionLifetime };
        return this;
    }

    /// <inheritdoc />
    public IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> KeepAliveMinInterval(TimeSpan keepAliveMinInterval)
    {
        state.MsmtOptionsValue = (state.MsmtOptionsValue ?? new()) with { KeepAliveMinInterval = keepAliveMinInterval };
        return this;
    }

    /// <inheritdoc />
    public IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> KeepAliveMaxInterval(TimeSpan keepAliveMaxInterval)
    {
        state.MsmtOptionsValue = (state.MsmtOptionsValue ?? new()) with { KeepAliveMaxInterval = keepAliveMaxInterval };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Encoding(HdlcEncoding encoding)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { Link = (state.HdlcOptionsValue ?? new()).Link with { Encoding = encoding } };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Crc(HdlcCrc crc)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { Link = (state.HdlcOptionsValue ?? new()).Link with { Crc = crc } };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> ReceiveClockSource(HdlcReceiveClockSource receiveClockSource)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { Link = (state.HdlcOptionsValue ?? new()).Link with { ReceiveClockSource = receiveClockSource } };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> TransmitClockSource(HdlcTransmitClockSource transmitClockSource)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { Link = (state.HdlcOptionsValue ?? new()).Link with { TransmitClockSource = transmitClockSource } };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> PhaseLockedLoopDivisor(HdlcPhaseLockedLoopDivisor phaseLockedLoopDivisor)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { Link = (state.HdlcOptionsValue ?? new()).Link with { PhaseLockedLoopDivisor = phaseLockedLoopDivisor } };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> ClockSpeed(int clockSpeed)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { Link = (state.HdlcOptionsValue ?? new()).Link with { ClockSpeed = clockSpeed } };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> IdlePattern(HdlcIdlePattern idlePattern)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { IdlePattern = idlePattern };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> PreambleLength(HdlcPreambleLength preambleLength)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { PreambleLength = preambleLength };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> PreamblePattern(HdlcPreamblePattern preamblePattern)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { PreamblePattern = preamblePattern };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> UnderrunAction(HdlcUnderrunAction underrunAction)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { UnderrunAction = underrunAction };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> DisablePollFinalBit(bool disablePollFinalBit)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { DisablePollFinalBit = disablePollFinalBit };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> MaxInfoField(int maxInfoField)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { MaxInfoField = maxInfoField };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> RetryInterval(TimeSpan? retryInterval)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { RetryInterval = retryInterval };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> RetransmitInterval(TimeSpan? retransmitInterval)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { RetransmitInterval = retransmitInterval };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> AcknowledgeDelay(TimeSpan acknowledgeDelay)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { AcknowledgeDelay = acknowledgeDelay };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> MaxRetransmissions(int? maxRetransmissions)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { MaxRetransmissions = maxRetransmissions };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> TransmitWindow(int transmitWindow)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { TransmitWindow = transmitWindow };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Loopback(bool loopback)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { Loopback = loopback };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> DetectDisconnect(bool detectDisconnect)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { DetectDisconnect = detectDisconnect };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> EnableMonitor(bool enableMonitor)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { EnableMonitor = enableMonitor };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> EnableSignals(bool enableSignals)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { EnableSignals = enableSignals };
        return this;
    }

    /// <inheritdoc />
    public IAddressTypeBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> AddressType(AddressType type)
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

    private FrameBuilder<TFrame, TPriority, TLevel, TAspect> RequireFrames() => frames ?? throw new InvalidOperationException("Frames<TProcessor>() must be called before the frame handlers are stated.");

    private PacketBuilder<TFrame, TPacket, TPriority> RequirePackets() => packets ?? throw new InvalidOperationException("Packets() must be called before the packet handlers are stated.");

    private void Complete()
    {
        if (frames is not null)
        {
            state.FrameMap = frames.Build();
            state.NetworkHandler = frames.NetworkHandler;
            state.FrameHandshakeProcessor = frames.HandshakeHandler;
        }

        if (packets is not null)
        {
            state.PacketMap = packets.Build();
            state.PacketHandshakeProcessor = packets.HandshakeHandler;
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
    }
}
