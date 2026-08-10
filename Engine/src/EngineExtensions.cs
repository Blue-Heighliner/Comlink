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
            services.TryAddSingleton(new EngineConfig());
            services.AddConventionSingletons();
            services.AddOpenFrameTransport();

            services.AddSingleton<IServiceConnection, DirectServiceConnection>();
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
    /// Registers the loaded engine configuration into the host's DI container before Engine defaults.
    /// Call this before <see cref="UseEngine"/> so that the config overrides Engine defaults via convention registration.
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

    /// <summary>Scans the Engine assembly and registers each concrete class as its <c>IThing</c> interface singleton using <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService,TImplementation}"/>.</summary>
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
            Type? iface = type.GetInterfaces()
                .FirstOrDefault(i => i.Name == $"I{type.Name}" && i.Namespace == type.Namespace);
            if (iface is null)
                continue;
            services.TryAddSingleton(iface, type);
        }
    }

}
