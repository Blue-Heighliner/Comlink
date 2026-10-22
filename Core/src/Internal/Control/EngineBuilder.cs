namespace BlueHeighliner.Comlink;

/// <summary>
/// Implements <see cref="IEngineBuilder"/> and holds what the typed <see cref="EngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}"/> it returns collects from the host in <see cref="IEngineConfiguration.Configure"/>.
/// Nothing is interpreted here; <see cref="EngineController"/> reads the collected state and applies the defaults for
/// whatever was left unstated.
/// </summary>
internal sealed class EngineBuilder : IEngineBuilder, IAsyncDisposable
{
    private readonly Lock identifierLock = new();
    private ServiceProvider? bootstrap;
    private IServiceProvider? handlerServices;
    private IConfigurationHandler? identifier;

    /// <summary>Work the typed builders still have to do once the configuration has finished stating things, such as turning what was collected into the frame mapping.</summary>
    public List<Action> Completions { get; } = [];
    /// <summary>Whether <see cref="Types{TFrame, TPriority, TLevel, TAspect, TProfile}"/> has been called.</summary>
    public bool AreTypesStated { get; private set; }
    /// <summary>The frame mapping, or <see langword="null"/> until <c>Frames</c> is called.</summary>
    public FrameMap? FrameMap { get; set; }
    /// <summary>The packet mapping, or <see langword="null"/> while packetization is off.</summary>
    public PacketMap? PacketMap { get; set; }
    /// <summary>The handler for the names and words the app shows, if stated.</summary>
    public ServiceRegistration<IDisplayHandler>? DisplayHandler { get; set; }
    /// <summary>The display handler, created once the configuration has finished, from the bootstrap container when there is one.</summary>
    public IDisplayHandler? DisplayHandlerInstance { get; private set; }
    /// <summary>Absolute path to the application data root, <c>%APPDATA%</c>, which everything the engine stores lives under.</summary>
    /// <exception cref="InvalidOperationException">The operating system names no absolute application data folder, which would otherwise put the data in the working directory.</exception>
    public string AppDataRoot => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) is { Length: > 0 } root && Path.IsPathRooted(root) ? root : throw new InvalidOperationException("The operating system gave no absolute application data folder, and nothing is stored in the working directory instead.");
    /// <summary>The name of the folder under <see cref="AppDataRoot"/> that holds the app's data: what the display handler states, otherwise the entry assembly's name.</summary>
    public string DataFolderName => DisplayHandlerInstance?.DataFolderName.OrNull() ?? Assembly.GetEntryAssembly()?.GetName().Name ?? "App";
    /// <summary>The folder the <c>--data</c> argument names, made absolute, or <see langword="null"/> when it is not given or the command line may not override.</summary>
    public string? DataFolderOverride { get; set; }
    /// <summary>Absolute path to the app's one data folder, which holds the network configuration file, the database, the logs and the stored records: the <c>--data</c> folder when given, otherwise <see cref="DataFolderName"/> under <see cref="AppDataRoot"/>.</summary>
    public string AppDataFolder => DataFolderOverride ?? Path.Combine(AppDataRoot, DataFolderName);
    /// <summary>The configured levels, in ascending order; empty when none were stated.</summary>
    public List<LevelDefinition> LevelValues { get; } = [];
    /// <summary>The configured aspects, in the order stated; empty when none were stated.</summary>
    public List<AspectDefinition> AspectValues { get; } = [];
    /// <summary>The handler that controls the alert alarm, if stated.</summary>
    public ServiceRegistration<IAlarmHandler>? AlarmHandler { get; set; }
    /// <summary>The largest payload of a packet, stated with the packets.</summary>
    public int? MaxPayloadSizeValue { get; set; }
    /// <summary>How many packets may be in flight at once, if stated.</summary>
    public int? PacketWindowValue { get; set; }
    /// <summary>The handler that controls how drafts are composed, if stated.</summary>
    public ServiceRegistration<IDraftFrameHandler>? DraftHandler { get; set; }
    /// <summary>The selectable priorities, empty when none were stated.</summary>
    public List<MessagePriorityOption> PriorityOptions { get; } = [];
    /// <summary>The overridden address type display labels, by address type.</summary>
    public Dictionary<AddressType, string> AddressTypeLabels { get; } = [];
    /// <summary>The handler that controls the print manager, if stated.</summary>
    public ServiceRegistration<IPrintPolicy>? PrintHandler { get; set; }
    /// <summary>The handler that controls the layout of log lines, if stated.</summary>
    public ServiceRegistration<ILogHandler>? LogHandler { get; set; }
    /// <summary>The handler that decides what may be deleted, if stated.</summary>
    public ServiceRegistration<IDeleteHandler>? DeleteHandler { get; set; }
    /// <summary>How the MSMT peer options are adjusted, if stated.</summary>
    public MsmtConnectionOptions? MsmtOptionsValue { get; set; }
    /// <summary>How the HDLC peer options are adjusted, if stated.</summary>
    public HdlcPeerOptions? HdlcOptionsValue { get; set; }
    /// <summary>The packet handshake handler, if stated.</summary>
    public ServiceRegistration<IHandshakeHandler>? PacketHandshakeHandler { get; set; }
    /// <summary>The frame handshake handler, if stated.</summary>
    public ServiceRegistration<IHandshakeHandler>? FrameHandshakeHandler { get; set; }
    /// <summary>The authority certificate file, if stated.</summary>
    public string? AuthorityCertificateValue { get; set; }
    /// <summary>The user certificate file, if stated.</summary>
    public string? UserCertificateValue { get; set; }
    /// <summary>Whether the command-line overrides (<c>--config</c>, <c>--authority</c>, <c>--certificate</c>, <c>--data</c> and <c>--log</c>) are honored.</summary>
    public bool AreCommandLineOverridesAllowed { get; set; }
    /// <summary>Whether a log entry that reports an exception also carries its stack trace; on unless stated.</summary>
    public bool IsStackTraceEnabled { get; set; } = true;
    /// <summary>The type each user's profile in the network configuration is read into, or <see langword="null"/> when the host stated <see cref="NoProfile"/>.</summary>
    public Type? UserProfileType { get; set; }
    /// <summary>The configuration handler, if stated.</summary>
    public ServiceRegistration<IConfigurationHandler>? ConfigurationHandler { get; set; }
    /// <summary>Whether the configuration handler states its own <see cref="IConfigurationHandler.Identify"/> rather than taking the default.</summary>
    public bool IsIdentifying { get; set; }
    /// <summary>The external systems.</summary>
    public List<IExternalSystem> ExternalSystems { get; } = [];
    /// <summary>Registers the services typed by the host's enums, once <c>Types</c> has stated them.</summary>
    public Action<IServiceCollection>? TypedServices { get; private set; }

    /// <summary>The handler that reacts to peer activity, if stated.</summary>
    public ServiceRegistration<IEngineFrameHandler>? FrameHandler { get; set; }
    /// <summary>The custom export formats, in the order added.</summary>
    public List<ServiceRegistration<IExportFormat>> ExportFormats { get; } = [];
    /// <summary>The custom import formats, in the order added.</summary>
    public List<ServiceRegistration<IImportSource>> ImportFormats { get; } = [];
    /// <summary>The custom auto forwarders, in the order added.</summary>
    public List<AutoForwarderDefinition> AutoForwarders { get; } = [];

    /// <summary>
    /// Constructs <typeparamref name="TConfiguration"/> from a bootstrap container, so it can take dependencies, then
    /// runs it against a new builder and checks the result. The bootstrap container holds logging plus whatever
    /// <paramref name="configureServices"/> registers, and lives until the returned builder is disposed, since the
    /// configuration may have handed the engine functions that use what it was given.
    /// </summary>
    /// <typeparam name="TConfiguration">The host's configuration type.</typeparam>
    /// <param name="configureServices">Registers the services the configuration depends on.</param>
    /// <param name="assemblies">The host's assemblies, whose classes are registered under the interface named for them unless the host registered that interface itself, or <see langword="null"/> for none.</param>
    /// <exception cref="InvalidOperationException">The configuration cannot be constructed, or is incomplete or contradicts itself.</exception>
    public static EngineBuilder Build<TConfiguration>(Action<IServiceCollection>? configureServices, IReadOnlyList<Assembly>? assemblies = null) where TConfiguration : class, IEngineConfiguration
    {
        ServiceCollection services = new();
        services.AddLogging();
        configureServices?.Invoke(services);
        services.TryAddSingleton<IDebugMode, DebugMode>();
        services.TryAddSingleton<IDataPool, DataPool>();
        services.TryAddSingleton<ILogger, Logger>();
        services.AddHostConventionSingletons(assemblies ?? []);
        services.AddSingleton(provider => provider.Create<TConfiguration>());
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
        foreach (Action completion in builder.Completions)
        {
            completion();
        }
        builder.Validate();
        builder.DisplayHandlerInstance = builder.DisplayHandler?.Create(services);
        builder.handlerServices = services;
        return builder;
    }

    /// <inheritdoc />
    public IEngineBuilder<TFrame, NoPacket, TPriority, TLevel, TAspect, TProfile> Types<TFrame, TPriority, TLevel, TAspect, TProfile>() where TFrame : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum where TProfile : class
        => Types<TFrame, NoPacket, TPriority, TLevel, TAspect, TProfile>();

    /// <inheritdoc />
    public IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Types<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>() where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum where TProfile : class
    {
        if (AreTypesStated)
        {
            throw new InvalidOperationException("The engine configuration states its types more than once.");
        }

        AreTypesStated = true;
        TypedServices = services =>
        {
            services.AddSingleton<IServiceConnection<TPriority, TLevel, TAspect, TProfile>>(provider => new ServiceConnection<TPriority, TLevel, TAspect, TProfile>(provider.GetRequiredService<IEngineConnection>()));
            services.AddSingleton<INetwork<TFrame, TPriority, TLevel, TAspect>>(provider => new Network<TFrame, TPriority, TLevel, TAspect>(provider.GetRequiredService<INetworkEnvironment>(), provider.GetRequiredService<IUserDirectory>()));
            services.AddSingleton<IUserCatalog<TLevel, TProfile>>(provider => new UserCatalog<TLevel, TProfile>(provider.GetRequiredService<IUserDirectory>()));
        };
        UserProfileType = typeof(TProfile) == typeof(NoProfile) ? null : typeof(TProfile);
        if (typeof(TPriority) == typeof(NoPriority))
        {
            PriorityOptions.AddRange(new PriorityBuilder<TPriority>().Priority(default).Build());
        }

        return new EngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>(this);
    }

    /// <summary>Says which user a certificate means: the configuration handler's answer when it states one, otherwise the first common name. The handler asked is built once, from the bootstrap container, since the engine does not exist yet.</summary>
    /// <param name="commonNames">The subject common names of the user certificate, never empty.</param>
    public string Identify(IReadOnlyList<string> commonNames)
    {
        if (!IsIdentifying || ConfigurationHandler is null)
        {
            return commonNames[0];
        }

        lock (identifierLock)
        {
            identifier ??= ConfigurationHandler.Create(handlerServices);
            return identifier.Identify(commonNames);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        switch (identifier)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }

        if (bootstrap is not null)
        {
            await bootstrap.DisposeAsync();
        }
    }

    private void Validate()
    {
        if (!AreTypesStated)
        {
            throw new InvalidOperationException("The engine configuration must state its types with Types<...>().");
        }
        if (PriorityOptions.Count == 0)
        {
            throw new InvalidOperationException("The engine configuration must state its priority levels, lowest first, with Priority(...).");
        }
        if (FrameMap is null)
        {
            throw new InvalidOperationException("The engine configuration must state its frame handlers with Frames<THandler>().");
        }
    }
}
