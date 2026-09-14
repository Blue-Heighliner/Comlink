namespace BlueHeighliner.Comlink;

/// <summary>Entry point that bootstraps the engine as a GUI application or a headless peer client.</summary>
[ExcludeFromCodeCoverage]
public static class Engine
{
    /// <summary>Engine configuration loaded from command-line arguments.</summary>
    internal static EngineConfig Config { get; private set; } = new();
    /// <summary>Host-provided callback to register DI services, including the required <see cref="IEngineController"/>.</summary>
    internal static Action<IServiceCollection> ConfigureServices { get; private set; } = _ => { };

    /// <summary>
    /// Loads configuration from <paramref name="args"/>, then starts the engine in Headless mode or GUI mode.
    /// </summary>
    /// <param name="args">Command-line arguments passed from the host entry point.</param>
    /// <param name="configureServices">
    /// Registers this host's DI services — must register an <see cref="IEngineController"/> implementation
    /// like any other service; <see cref="Engine"/> resolves it from the same container this populates.
    /// </param>
    public static async Task Start(string[] args, Action<IServiceCollection> configureServices)
    {
        ConfigureServices = configureServices;

        IEngineController controller = ResolveEngineController();
        Config = controller.ConfigFileEnabled ? EngineConfig.Load(args) : new EngineConfig();

        if (Config.HeadlessMode)
        {
            await RunHeadless();
        }
        else
        {
            RunGui(args);
        }
    }

    /// <summary>
    /// Resolves <see cref="IEngineController"/> from a minimal, throwaway service provider built from
    /// <see cref="ConfigureServices"/> alone — <see cref="Config"/> does not exist yet at this point, so
    /// this must happen before the real host container (which depends on <see cref="Config"/>) is built.
    /// Only <see cref="IEngineController.ConfigFileEnabled"/> is actually consulted; every other member is
    /// left unused for this bootstrap resolution.
    /// </summary>
    private static IEngineController ResolveEngineController()
    {
        ServiceCollection services = new();
        ConfigureServices(services);
        services.TryAddSingleton<ICurrentUserProvider, CurrentUserProvider>();
        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IEngineController>();
    }

    private static async Task RunHeadless()
        => await Host.CreateDefaultBuilder()
            .UseEngineConfig(Config)
            .UseEngine(EngineMode.Headless)
            .ConfigureServices((_, services) => ConfigureServices(services))
            .UseEngineConfigOverrides()
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Information))
            .Build()
            .RunAsync();

    private static void RunGui(string[] args)
        => AppBuilder.Configure<EngineApp>()
            .UsePlatformDetect()
            // Render popups (e.g. the fill-in options dropdown) inside the owning window's own
            // surface instead of as separate X11 windows — avoids a class of Avalonia-on-X11 bugs
            // where a popup's GPU surface gets stuck and stops repainting until the process restarts.
            .With(new X11PlatformOptions { OverlayPopups = true })
            .UseEngineStyles()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
}
