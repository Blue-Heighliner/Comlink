namespace BlueHeighliner.Comlink;

/// <summary>Implements <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}"/> and every sub-configuration chained off it (frames, packets, priorities and levels), recording what the host states in the <see cref="EngineBuilder"/> that created it. They are one object, so a sub-configuration continues straight into the settings of the engine builder.</summary>
internal sealed class EngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>, IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>, IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>, IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>, ILevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>, IAspectBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>, IAddressTypeBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>, IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>, IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum where TProfile : class
{
    private readonly EngineBuilder state;
    private readonly PriorityBuilder<TPriority> priorities = new();
    private readonly LevelBuilder<TLevel> levels = new();
    private readonly AspectBuilder<TAspect> aspects = new();
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
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Frames<THandler>() where THandler : IFrameHandler<TFrame, TPriority, TLevel, TAspect>
    {
        frames ??= new();
        frames.Handler<THandler>();
        return this;
    }

    /// <inheritdoc />
    public IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Packets<THandler>(int maxPayloadSize) where THandler : IPacketHandler<TFrame, TPacket>
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
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Display<THandler>() where THandler : IDisplayHandler
    {
        state.DisplayHandler = ServiceRegistration<IDisplayHandler>.Of(typeof(THandler), instance => (IDisplayHandler)instance);
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Alarms<THandler>() where THandler : IAlarmHandler
    {
        state.AlarmHandler = ServiceRegistration<IAlarmHandler>.Of(typeof(THandler), instance => (IAlarmHandler)instance);
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Drafts<THandler>() where THandler : IDraftHandler<TPriority, TLevel, TAspect>
    {
        state.DraftHandler = ServiceRegistration<IDraftFrameHandler>.Of(typeof(THandler), instance => new DraftFrameHandler<TPriority, TLevel, TAspect>((IDraftHandler<TPriority, TLevel, TAspect>)instance));
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Logs<THandler>() where THandler : ILogHandler
    {
        state.LogHandler = ServiceRegistration<ILogHandler>.Of(typeof(THandler), instance => (ILogHandler)instance);
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Prints<THandler>() where THandler : IPrintHandler<TPriority, TLevel, TAspect>
    {
        state.PrintHandler = ServiceRegistration<IPrintPolicy>.Of(typeof(THandler), instance => new PrintPolicy<TPriority, TLevel, TAspect>((IPrintHandler<TPriority, TLevel, TAspect>)instance));
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Deletes<THandler>() where THandler : IDeleteHandler
    {
        state.DeleteHandler = ServiceRegistration<IDeleteHandler>.Of(typeof(THandler), instance => (IDeleteHandler)instance);
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> StackTrace(bool enabled)
    {
        state.IsStackTraceEnabled = enabled;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Configure<TConfigurationHandler>() where TConfigurationHandler : class, IConfigurationHandler
    {
        state.ConfigurationHandler = ServiceRegistration<IConfigurationHandler>.Of(typeof(TConfigurationHandler), instance => (IConfigurationHandler)instance);
        InterfaceMapping map = typeof(TConfigurationHandler).GetInterfaceMap(typeof(IConfigurationHandler));
        state.IsIdentifying = map.TargetMethods[Array.FindIndex(map.InterfaceMethods, method => method.Name == nameof(IConfigurationHandler.Identify))] is { DeclaringType.IsInterface: false };
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Certificates(string authorityPath, string userPath)
    {
        state.AuthorityCertificateValue = authorityPath;
        state.UserCertificateValue = userPath;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> CommandLineOverrides(bool allowed)
    {
        state.AreCommandLineOverridesAllowed = allowed;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> ExternalSystem(IExternalSystem system)
    {
        if (!state.ExternalSystems.Contains(system))
        {
            state.ExternalSystems.Add(system);
        }
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Export<TFormat>() where TFormat : IExportFormat
    {
        state.ExportFormats.Add(ServiceRegistration<IExportFormat>.Of(typeof(TFormat), instance => (IExportFormat)instance));
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Import<TFormat>() where TFormat : IImportFormat<TPriority, TLevel>
    {
        state.ImportFormats.Add(ServiceRegistration<IImportSource>.Of(typeof(TFormat), instance => new ImportSource<TPriority, TLevel>((IImportFormat<TPriority, TLevel>)instance)));
        return this;
    }

    /// <inheritdoc cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.AutoForwarder"/>
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> AutoForwarder(string name)
    {
        if (state.AutoForwarders.All(forwarder => forwarder.Name != name))
        {
            state.AutoForwarders.Add(new() { Name = name });
        }

        return this;
    }

    /// <inheritdoc cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Create"/>
    public IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Create(Func<TFrame> create)
    {
        RequireFrames().Create(create);
        return this;
    }

    /// <inheritdoc />
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>.Heartbeat<THandler>()
    {
        RequireFrames().Heartbeat<THandler>();
        return this;
    }

    /// <inheritdoc />
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>.Serializer<TSerializer>()
    {
        RequireFrames().Serializer<TSerializer>();
        return this;
    }

    /// <inheritdoc />
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>.Heartbeat<THandler>()
    {
        RequirePackets().Heartbeat<THandler>();
        return this;
    }

    /// <inheritdoc />
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>.Serializer<TSerializer>()
    {
        RequirePackets().Serializer<TSerializer>();
        return this;
    }

    /// <inheritdoc />
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>.Handshake<THandler>()
    {
        RequireFrames().Handshake<THandler>();
        return this;
    }

    /// <inheritdoc />
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>.Handshake<THandler>()
    {
        RequirePackets().Handshake<THandler>();
        return this;
    }

    /// <inheritdoc />
    public IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Window(int packets)
    {
        state.PacketWindowValue = packets;
        return this;
    }

    /// <inheritdoc />
    public IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Priority(TPriority priority)
    {
        priorities.Priority(priority);
        return this;
    }

    /// <inheritdoc />
    IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>.IsHidden(bool isHidden)
    {
        priorities.IsHidden(isHidden);
        return this;
    }

    /// <inheritdoc />
    ILevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> ILevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>.IsHidden(bool isHidden)
    {
        levels.IsHidden(isHidden);
        return this;
    }

    /// <inheritdoc />
    IAspectBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> IAspectBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>.IsHidden(bool isHidden)
    {
        aspects.IsHidden(isHidden);
        return this;
    }

    /// <inheritdoc />
    IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>.Label(string label)
    {
        priorities.Label(label);
        return this;
    }

    /// <inheritdoc />
    public IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Msmt() => this;

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Hdlc() => this;

    /// <inheritdoc />
    public IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> HandshakeTimeout(TimeSpan? handshakeTimeout)
    {
        state.MsmtOptionsValue = (state.MsmtOptionsValue ?? new()) with { HandshakeTimeout = handshakeTimeout };
        return this;
    }

    /// <inheritdoc />
    public IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> StallTimeout(TimeSpan? stallTimeout)
    {
        state.MsmtOptionsValue = (state.MsmtOptionsValue ?? new()) with { StallTimeout = stallTimeout };
        return this;
    }

    /// <inheritdoc />
    public IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> ResponseTimeout(TimeSpan? responseTimeout)
    {
        state.MsmtOptionsValue = (state.MsmtOptionsValue ?? new()) with { ResponseTimeout = responseTimeout };
        return this;
    }

    /// <inheritdoc />
    public IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> TcpKeepAliveTime(TimeSpan? tcpKeepAliveTime)
    {
        state.MsmtOptionsValue = (state.MsmtOptionsValue ?? new()) with { TcpKeepAliveTime = tcpKeepAliveTime };
        return this;
    }

    /// <inheritdoc />
    public IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> MaximumSessionLifetime(TimeSpan maximumSessionLifetime)
    {
        state.MsmtOptionsValue = (state.MsmtOptionsValue ?? new()) with { MaximumSessionLifetime = maximumSessionLifetime };
        return this;
    }

    /// <inheritdoc />
    public IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> SessionLifetime(TimeSpan sessionLifetime)
    {
        state.MsmtOptionsValue = (state.MsmtOptionsValue ?? new()) with { SessionLifetime = sessionLifetime };
        return this;
    }

    /// <inheritdoc />
    public IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> KeepAliveMinInterval(TimeSpan keepAliveMinInterval)
    {
        state.MsmtOptionsValue = (state.MsmtOptionsValue ?? new()) with { KeepAliveMinInterval = keepAliveMinInterval };
        return this;
    }

    /// <inheritdoc />
    public IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> KeepAliveMaxInterval(TimeSpan keepAliveMaxInterval)
    {
        state.MsmtOptionsValue = (state.MsmtOptionsValue ?? new()) with { KeepAliveMaxInterval = keepAliveMaxInterval };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Encoding(HdlcEncoding encoding)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { Link = (state.HdlcOptionsValue ?? new()).Link with { Encoding = encoding } };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Crc(HdlcCrc crc)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { Link = (state.HdlcOptionsValue ?? new()).Link with { Crc = crc } };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> ReceiveClockSource(HdlcReceiveClockSource receiveClockSource)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { Link = (state.HdlcOptionsValue ?? new()).Link with { ReceiveClockSource = receiveClockSource } };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> TransmitClockSource(HdlcTransmitClockSource transmitClockSource)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { Link = (state.HdlcOptionsValue ?? new()).Link with { TransmitClockSource = transmitClockSource } };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> PhaseLockedLoopDivisor(HdlcPhaseLockedLoopDivisor phaseLockedLoopDivisor)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { Link = (state.HdlcOptionsValue ?? new()).Link with { PhaseLockedLoopDivisor = phaseLockedLoopDivisor } };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> ClockSpeed(int clockSpeed)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { Link = (state.HdlcOptionsValue ?? new()).Link with { ClockSpeed = clockSpeed } };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> IdlePattern(HdlcIdlePattern idlePattern)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { IdlePattern = idlePattern };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> PreambleLength(HdlcPreambleLength preambleLength)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { PreambleLength = preambleLength };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> PreamblePattern(HdlcPreamblePattern preamblePattern)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { PreamblePattern = preamblePattern };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> UnderrunAction(HdlcUnderrunAction underrunAction)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { UnderrunAction = underrunAction };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> DisablePollFinalBit(bool disablePollFinalBit)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { DisablePollFinalBit = disablePollFinalBit };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> MaxInfoField(int maxInfoField)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { MaxInfoField = maxInfoField };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> RetryInterval(TimeSpan? retryInterval)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { RetryInterval = retryInterval };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> RetransmitInterval(TimeSpan? retransmitInterval)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { RetransmitInterval = retransmitInterval };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> AcknowledgeDelay(TimeSpan acknowledgeDelay)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { AcknowledgeDelay = acknowledgeDelay };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> MaxRetransmissions(int? maxRetransmissions)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { MaxRetransmissions = maxRetransmissions };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> TransmitWindow(int transmitWindow)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { TransmitWindow = transmitWindow };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Loopback(bool loopback)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { Loopback = loopback };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> DetectDisconnect(bool detectDisconnect)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { DetectDisconnect = detectDisconnect };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> EnableMonitor(bool enableMonitor)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { EnableMonitor = enableMonitor };
        return this;
    }

    /// <inheritdoc />
    public IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> EnableSignals(bool enableSignals)
    {
        state.HdlcOptionsValue = (state.HdlcOptionsValue ?? new()) with { EnableSignals = enableSignals };
        return this;
    }

    /// <inheritdoc />
    public IAddressTypeBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> AddressType(AddressType type)
    {
        currentAddressType = type;
        return this;
    }

    /// <inheritdoc />
    IAddressTypeBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> IAddressTypeBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>.Label(string label)
    {
        state.AddressTypeLabels[currentAddressType] = label;
        return this;
    }

    /// <inheritdoc />
    public IAspectBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Aspect(TAspect aspect)
    {
        aspects.Aspect(aspect);
        return this;
    }

    /// <inheritdoc />
    IAspectBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> IAspectBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>.Label(string label)
    {
        aspects.Label(label);
        return this;
    }

    /// <inheritdoc />
    public ILevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Level(TLevel level)
    {
        levels.Level(level);
        return this;
    }

    /// <inheritdoc />
    public ILevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Background(string background)
    {
        levels.Background(background);
        return this;
    }

    /// <inheritdoc />
    public ILevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Foreground(string foreground)
    {
        levels.Foreground(foreground);
        return this;
    }

    /// <inheritdoc />
    ILevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> ILevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>.Label(string label)
    {
        levels.Label(label);
        return this;
    }

    private FrameBuilder<TFrame, TPriority, TLevel, TAspect> RequireFrames() => frames ?? throw new InvalidOperationException("Frames<THandler>() must be called before the frame handlers are stated.");

    private PacketBuilder<TFrame, TPacket, TPriority> RequirePackets() => packets ?? throw new InvalidOperationException("Packets() must be called before the packet handlers are stated.");

    private void Complete()
    {
        if (frames is not null)
        {
            state.FrameMap = frames.Build();
            state.FrameHandler = frames.FrameHandler;
            state.FrameHandshakeHandler = frames.HandshakeHandler;
        }

        if (packets is not null)
        {
            state.PacketMap = packets.Build();
            state.PacketHandshakeHandler = packets.HandshakeHandler;
        }

        List<LevelDefinition> builtLevels = levels.Build();
        if (builtLevels.Count > 0)
        {
            state.LevelValues.Clear();
            state.LevelValues.AddRange(builtLevels);
        }

        List<AspectDefinition> builtAspects = aspects.Build();
        if (builtAspects.Count > 0)
        {
            state.AspectValues.Clear();
            state.AspectValues.AddRange(builtAspects);
        }

        List<MessagePriorityOption> builtPriorities = priorities.Build();
        if (builtPriorities.Count > 0)
        {
            state.PriorityOptions.Clear();
            state.PriorityOptions.AddRange(builtPriorities);
        }
    }
}
