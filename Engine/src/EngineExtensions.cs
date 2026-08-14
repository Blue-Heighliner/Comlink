namespace BlueHeighliner.Comlink.Engine;

/// <summary>Specifies the operating mode for the Engine host.</summary>
public enum EngineMode
{
    /// <summary>Runs the full GUI client with local database and UI.</summary>
    Client,
    /// <summary>Runs headless — as a normal peer client, with local database and no UI.</summary>
    Headless
}

/// <summary>Extension methods for wiring the Engine into a .NET generic host.</summary>
[ExcludeFromCodeCoverage]
public static class EngineExtensions
{
    /// <summary>
    /// Registers all Engine services, repositories, ViewModels, and infrastructure into the host's DI container.
    /// </summary>
    /// <param name="builder">The host builder to configure.</param>
    /// <param name="mode">Whether to run as a GUI client or headless peer client.</param>
    public static IHostBuilder UseEngine(this IHostBuilder builder, EngineMode mode)
    {
        return builder.ConfigureServices((_, services) =>
        {
            services.AddSingleton(typeof(EngineMode), mode);

            EngineConfig? preConfig = services.FirstOrDefault(d => d.ServiceType == typeof(EngineConfig))?.ImplementationInstance as EngineConfig;
            switch (preConfig?.GetNodeRole() ?? NodeRole.Peer)
            {
                case NodeRole.Client:
                    services.AddSingleton<IPeerService, ClientPeerService>();
                    break;
                case NodeRole.Server:
                    services.AddSingleton<IPeerService, ServerRoutingService>();
                    break;
            }

            services.TryAddSingleton(new EngineConfig());
            services.AddConventionSingletons();
            services.AddOpenFrameTransport();

            services.AddSingleton<IServiceConnection, DirectServiceConnection>();
            // ILinePrinter has no same-named implementing class (DefaultPrinterProvider implements both
            // IPrinterProvider and ILinePrinter), so the convention scanner only picks up IPrinterProvider.
            services.TryAddSingleton<ILinePrinter, DefaultPrinterProvider>();
            if (mode == EngineMode.Client)
                services.TryAddSingleton<IBodyDocumentFactory, BodyDocumentFactory>();

            services.AddHostedService<EngineHost>();
        }).ConfigureLogging((_, logging) =>
        {
            logging.ClearProviders();
            logging.AddFilter("Microsoft", LogLevel.None);
            logging.AddFilter("System", LogLevel.None);
            logging.Services.AddSingleton<ILoggerProvider, DailyFileLoggerProvider>();
            if (mode == EngineMode.Client)
                logging.Services.AddSingleton<ILoggerProvider, ActivityLoggerProvider>();
        });
    }

    /// <summary>
    /// Registers the loaded engine configuration into the host's DI container. Call this before
    /// <see cref="UseEngine"/> — it also directly inspects <see cref="EngineConfig.NodeRole"/> at
    /// composition time, before the container exists, to select the right <see cref="Peer.IPeerService"/>
    /// implementation.
    /// </summary>
    /// <param name="builder">The host builder to configure.</param>
    /// <param name="config">The loaded engine configuration.</param>
    public static IHostBuilder UseEngineConfig(this IHostBuilder builder, EngineConfig config)
    {
        return builder.ConfigureServices((_, services) =>
        {
            services.AddSingleton(config);
        });
    }

    /// <summary>
    /// Applies <see cref="EngineConfig"/> overrides on top of every control-interface implementation that
    /// has a corresponding <c>config.json</c> field, using whichever implementation is currently registered
    /// for each interface — the Engine default, or a host override registered via its own
    /// <c>ConfigureServices</c> callback. Call this last, after every other <c>ConfigureServices</c> call
    /// (including <see cref="UseEngine"/> and any host callback that registers control-interface overrides),
    /// so it sees the final registration for each interface. See <c>Docs/Control.md</c>.
    /// </summary>
    /// <param name="builder">The host builder to configure.</param>
    public static IHostBuilder UseEngineConfigOverrides(this IHostBuilder builder)
    {
        return builder.ConfigureServices((_, services) =>
        {
            services.ApplyConfigOverride<IAlertSettings>((fallback, config) => new ConfiguredAlertSettings(fallback, config));
            services.ApplyConfigOverride<IAppSettings>((fallback, config) => new ConfiguredAppSettings(fallback, config));
            services.ApplyConfigOverride<IMessageComposition>((fallback, config) => new ConfiguredMessageComposition(fallback, config));
            services.ApplyConfigOverride<INetworkTopology>((fallback, config) => new ConfiguredNetworkTopology(fallback, config));
            services.ApplyConfigOverride<IOftPeerCertificateName>((fallback, config) => new ConfiguredOftPeerCertificateName(fallback, config));
            services.ApplyConfigOverride<IPortConfiguration>((fallback, config) => new ConfiguredPortConfiguration(fallback, config));
            services.ApplyConfigOverride<IPrintPolicy>((fallback, config) => new ConfiguredPrintPolicy(fallback, config));
            services.ApplyConfigOverride<IUserDirectory>((fallback, config) => new ConfiguredUserDirectory(fallback, config));
            services.ApplyConfigOverride<IUserIdentity>((fallback, config) => new ConfiguredUserIdentity(fallback, config));
        });
    }

    /// <summary>
    /// Scans the Engine assembly and registers each concrete class as its <c>IThing</c> interface singleton
    /// using <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService,TImplementation}"/>.
    /// A class named <c>DefaultThing</c> also matches <c>IThing</c> — Engine's own default control-interface
    /// implementations follow that naming so a host can inherit from them (see <c>Docs/Control.md</c>).
    /// </summary>
    private static void AddConventionSingletons(this IServiceCollection services)
    {
        Assembly assembly = typeof(EngineExtensions).Assembly;
        foreach (Type type in assembly.GetTypes())
        {
            if (type.IsAbstract || !type.IsClass || type.IsNested || type.IsGenericTypeDefinition)
                continue;
            // Exclude entry ViewModels: constructed with new() using entity arguments, not resolvable from DI
            if (type.Namespace == "BlueHeighliner.Comlink.Engine.ViewModels.Entries")
                continue;
            string coreName = type.Name.StartsWith("Default", StringComparison.Ordinal) ? type.Name["Default".Length..] : type.Name;
            Type? iface = type.GetInterfaces()
                .FirstOrDefault(i => i.Name == $"I{coreName}" && i.Namespace == type.Namespace);
            if (iface is null)
                continue;
            services.TryAddSingleton(iface, type);
        }
    }

    /// <summary>
    /// Converts the currently-registered <typeparamref name="TInterface"/> implementation into a keyed
    /// "fallback" registration, then registers <paramref name="decorate"/>'s result as the new unkeyed
    /// <typeparamref name="TInterface"/> singleton, wrapping that fallback with <see cref="EngineConfig"/>
    /// overrides. If more than one <typeparamref name="TInterface"/> registration exists (e.g. both the
    /// Engine default and a host override), only the last one — the one that would otherwise win plain
    /// singular resolution — becomes the fallback; earlier ones are discarded.
    /// </summary>
    private static void ApplyConfigOverride<TInterface>(this IServiceCollection services, Func<TInterface, EngineConfig, TInterface> decorate)
        where TInterface : class
    {
        List<ServiceDescriptor> existing = [.. services.Where(d => d.ServiceType == typeof(TInterface))];
        ServiceDescriptor fallback = existing[^1];
        foreach (ServiceDescriptor descriptor in existing)
            services.Remove(descriptor);

        services.Add(new ServiceDescriptor(typeof(TInterface), ConfigOverrideFallbackKey, fallback.ImplementationType!, fallback.Lifetime));
        services.AddSingleton<TInterface>(sp => decorate((TInterface)sp.GetRequiredKeyedService(typeof(TInterface), ConfigOverrideFallbackKey), sp.GetRequiredService<EngineConfig>()));
    }

    private const string ConfigOverrideFallbackKey = "fallback";
}
