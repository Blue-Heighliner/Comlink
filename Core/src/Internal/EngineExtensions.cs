namespace BlueHeighliner.Comlink;

/// <summary>Extension methods for wiring the Engine into a .NET generic host.</summary>
[ExcludeFromCodeCoverage]
internal static class EngineExtensions
{
    /// <summary>
    /// Registers all Engine services, repositories, ViewModels, and infrastructure into the host's DI container,
    /// including the <see cref="IEngineController"/> built from what the host stated and the configuration file.
    /// </summary>
    /// <param name="builder">The host builder to configure.</param>
    /// <param name="mode">Whether to run as a GUI client or headless peer client.</param>
    /// <param name="engine">What the host stated in its <see cref="IEngineConfiguration"/>.</param>
    /// <param name="network">The loaded network configuration: the users of the network and the node settings for this process.</param>
    public static IHostBuilder UseEngine(this IHostBuilder builder, EngineMode mode, EngineBuilder engine, NetworkConfig network)
    {
        return builder.ConfigureServices((_, services) =>
        {
            services.AddSingleton(typeof(EngineMode), mode);
            services.AddSingleton(engine);
            services.AddSingleton(network);
            services.AddSingleton<ICurrentUserProvider, CurrentUserProvider>();
            services.AddSingleton<IEngineController>(sp =>
            {
                ICurrentUserProvider currentUser = sp.GetRequiredService<ICurrentUserProvider>();
                return new ConfiguredEngineController(new EngineController(engine, currentUser, network), network, currentUser);
            });

            services.AddSingleton<RolePeerService>();
            services.AddSingleton<IPeerService>(sp => sp.GetRequiredService<RolePeerService>());
            services.AddSingleton<IConnectionStatusService>(sp => sp.GetRequiredService<RolePeerService>());

            services.AddConventionSingletons();
            services.AddMsmt();
            services.TryAddSingleton<IMicroGatePeerFactory, MicroGatePeerFactory>();

            services.AddSingleton<IServiceConnection, DirectServiceConnection>();
            if (mode == EngineMode.Client)
            {
                services.TryAddSingleton<IBodyDocumentFactory, BodyDocumentFactory>();
            }

            services.AddHostedService<EngineHost>();
        }).ConfigureLogging((_, logging) =>
        {
            logging.ClearProviders();
            logging.AddFilter("Microsoft", LogLevel.None);
            logging.AddFilter("System", LogLevel.None);
            logging.Services.AddSingleton<ILoggerProvider, DailyFileLoggerProvider>();
            if (mode == EngineMode.Client)
            {
                logging.Services.AddSingleton<ILoggerProvider, ActivityLoggerProvider>();
            }
        });
    }

    /// <summary>
    /// Scans the Engine assembly and registers each concrete class as its <c>IThing</c> interface singleton
    /// using <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService,TImplementation}"/>.
    /// </summary>
    private static void AddConventionSingletons(this IServiceCollection services)
    {
        Assembly assembly = typeof(EngineExtensions).Assembly;
        foreach (Type type in assembly.GetTypes())
        {
            if (type.IsAbstract || !type.IsClass || type.IsNested || type.IsGenericTypeDefinition)
            {
                continue;
            }
            // Exclude entry ViewModels: constructed with new() using entity arguments, not resolvable from DI
            if (type.Namespace == "BlueHeighliner.Comlink.ViewModels.Entries")
            {
                continue;
            }
            Type? iface = type.GetInterfaces()
                .FirstOrDefault(i => i.Name == $"I{type.Name}" && i.Namespace == type.Namespace);
            if (iface is null)
            {
                continue;
            }
            services.TryAddSingleton(iface, type);
        }
    }
}
