namespace BlueHeighliner.Comlink;

/// <summary>
/// Implements <see cref="IEngineBuilder"/> and holds what the typed <see cref="EngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}"/> it returns collects from the host in <see cref="IEngineConfiguration.Configure"/>.
/// Nothing is interpreted here; <see cref="EngineController"/> reads the collected state and applies the defaults for
/// whatever was left unstated.
/// </summary>
internal sealed class EngineBuilder : IEngineBuilder, IAsyncDisposable
{
    private ServiceProvider? bootstrap;

    /// <summary>Work the typed builders still have to do once the configuration has finished stating things, such as turning what was collected into the frame mapping.</summary>
    public List<Action> Completions { get; } = [];
    /// <summary>Whether <see cref="Types{TFrame, TPriority, TLevel, TAspect}"/> has been called.</summary>
    public bool AreTypesStated { get; private set; }
    /// <summary>The frame mapping, or <see langword="null"/> until <c>Frames</c> is called.</summary>
    public FrameMap? FrameMap { get; set; }
    /// <summary>The packet mapping, or <see langword="null"/> while packetization is off.</summary>
    public PacketMap? PacketMap { get; set; }
    /// <summary>The handler for the names and words the app shows, if stated.</summary>
    public ServiceRegistration<IDisplayHandler>? DisplayHandler { get; set; }
    /// <summary>The display handler, created once the configuration has finished, from the bootstrap container when there is one.</summary>
    public IDisplayHandler? DisplayHandlerInstance { get; private set; }
    /// <summary>The configured message levels, in ascending order; empty when none were stated.</summary>
    public List<MessageLevel> MessageLevelValues { get; } = [];
    /// <summary>The configured message aspects, in the order stated; empty when none were stated.</summary>
    public List<MessageAspect> MessageAspectValues { get; } = [];
    /// <summary>The handler that controls the alert alarm, if stated.</summary>
    public ServiceRegistration<IAlarmHandler>? AlarmHandler { get; set; }
    /// <summary>The largest serialized packet, if stated.</summary>
    public int? PacketSizeValue { get; set; }
    /// <summary>How many packets may be in flight at once, if stated.</summary>
    public int? PacketWindowValue { get; set; }
    /// <summary>The handler that controls how drafts are composed, if stated.</summary>
    public ServiceRegistration<IDraftFrameHandler>? DraftHandler { get; set; }
    /// <summary>The selectable priorities, empty when none were stated.</summary>
    public List<MessagePriorityOption> PriorityOptions { get; } = [];
    /// <summary>The blocked tag and priority combinations.</summary>
    public List<TagPriorityBlock> BlockedCombinations { get; } = [];
    /// <summary>The overridden address type display labels, by address type.</summary>
    public Dictionary<AddressType, string> AddressTypeLabels { get; } = [];
    /// <summary>The handler that controls the print manager, if stated.</summary>
    public ServiceRegistration<IPrintFrameHandler>? PrintHandler { get; set; }
    /// <summary>The handler that controls the layout of log lines, if stated.</summary>
    public ServiceRegistration<ILogHandler>? LogHandler { get; set; }
    /// <summary>The handler that decides what may be deleted, if stated.</summary>
    public ServiceRegistration<IDeleteHandler>? DeleteHandler { get; set; }
    /// <summary>How the MSMT peer options are adjusted, if stated.</summary>
    public MsmtConnectionOptions? MsmtOptionsValue { get; set; }
    /// <summary>How the HDLC peer options are adjusted, if stated.</summary>
    public HdlcPeerOptions? HdlcOptionsValue { get; set; }
    /// <summary>The initial packet processor, if stated.</summary>
    public ServiceRegistration<IInitialProcessor>? InitialPacketProcessor { get; set; }
    /// <summary>The initial message processor, if stated.</summary>
    public ServiceRegistration<IInitialProcessor>? InitialFrameProcessor { get; set; }
    /// <summary>Whether the <c>--config</c> and <c>--user</c> arguments are honored.</summary>
    public bool AreCommandLineOverridesAllowed { get; set; }
    /// <summary>The external systems.</summary>
    public List<IExternalSystem> ExternalSystems { get; } = [];
    /// <summary>The processor that reacts to peer activity, if stated.</summary>
    public ServiceRegistration<INetworkHandler>? NetworkHandler { get; set; }
    /// <summary>The custom export formats, in the order added.</summary>
    public List<ServiceRegistration<IExportFormat>> ExportFormats { get; } = [];
    /// <summary>The custom import formats, in the order added.</summary>
    public List<ServiceRegistration<IImportFormat>> ImportFormats { get; } = [];
    /// <summary>The custom auto forward controllers, in the order added.</summary>
    public List<ServiceRegistration<AutoForwardControllerDefinition>> AutoForwardControllers { get; } = [];

    /// <summary>
    /// Constructs <typeparamref name="TConfiguration"/> from a bootstrap container, so it can take dependencies, then
    /// runs it against a new builder and checks the result. The bootstrap container holds logging plus whatever
    /// <paramref name="configureServices"/> registers, and lives until the returned builder is disposed, since the
    /// configuration may have handed the engine functions that use what it was given.
    /// </summary>
    /// <typeparam name="TConfiguration">The host's configuration type.</typeparam>
    /// <param name="configureServices">Registers the services the configuration depends on.</param>
    /// <exception cref="InvalidOperationException">The configuration cannot be constructed, or is incomplete or contradicts itself.</exception>
    public static EngineBuilder Build<TConfiguration>(Action<IServiceCollection>? configureServices) where TConfiguration : class, IEngineConfiguration
    {
        ServiceCollection services = new();
        services.AddLogging();
        configureServices?.Invoke(services);
        services.AddSingleton<TConfiguration>();
        ServiceProvider provider = services.BuildServiceProvider();
        try
        {
            EngineBuilder builder = Build(provider.GetRequiredService<TConfiguration>(), provider);
            builder.bootstrap = provider;
            return builder;
        }
        catch
        {
            provider.Dispose();
            throw;
        }
    }

    /// <summary>Runs <paramref name="configuration"/> against a new builder, checks the result and creates the display handler.</summary>
    /// <param name="configuration">The host's configuration.</param>
    /// <param name="services">The container the display handler is created from, which exists before the engine does and holds the logging and the services the host registered, or <see langword="null"/> for none.</param>
    /// <exception cref="InvalidOperationException">The configuration is incomplete or contradicts itself.</exception>
    public static EngineBuilder Build(IEngineConfiguration configuration, IServiceProvider? services = null)
    {
        EngineBuilder builder = new();
        configuration.Configure(builder);
        foreach (Action completion in builder.Completions) { completion(); }
        builder.Validate();
        builder.DisplayHandlerInstance = builder.DisplayHandler?.Create(services);
        return builder;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, NoPacket, TPriority, TLevel, TAspect> Types<TFrame, TPriority, TLevel, TAspect>() where TFrame : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
        => Types<TFrame, NoPacket, TPriority, TLevel, TAspect>();

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Types<TFrame, TPacket, TPriority, TLevel, TAspect>() where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
    {
        if (AreTypesStated) { throw new InvalidOperationException("The engine configuration states its types more than once."); }

        AreTypesStated = true;
        if (typeof(TPriority) == typeof(NoPriority)) { PriorityOptions.AddRange(new PriorityBuilder<TPriority>().Priority(default).Build()); }

        return new EngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect>(this);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => bootstrap?.DisposeAsync() ?? ValueTask.CompletedTask;

    private void Validate()
    {
        if (!AreTypesStated) { throw new InvalidOperationException("The engine configuration must state its types with Types<...>()."); }
        if (PriorityOptions.Count == 0) { throw new InvalidOperationException("The engine configuration must state its priority levels, lowest first, with Priorities().Priority(...)."); }
        if (FrameMap is null) { throw new InvalidOperationException("The engine configuration must state its frame handlers with Frames(...)."); }
    }
}
