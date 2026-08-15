namespace BlueHeighliner.Comlink.Engine;

/// <summary>Entry point helper that bootstraps the Engine as a GUI application or a headless peer client.</summary>
[ExcludeFromCodeCoverage]
public static class EngineApplication
{
    /// <summary>Engine configuration loaded from command-line arguments.</summary>
    internal static EngineConfig Config { get; private set; } = new();
    /// <summary>Host-provided callback to register additional DI services.</summary>
    internal static Action<IServiceCollection>? ConfigureServices { get; private set; }
    /// <summary>Optional <c>avares://</c> URI of the window icon to apply to the main window.</summary>
    internal static Uri? WindowIconUri { get; private set; }

    /// <summary>
    /// Loads configuration from <paramref name="args"/>, then starts the Engine in Headless mode or GUI mode.
    /// </summary>
    /// <typeparam name="TEngineController">
    /// The host's <see cref="IEngineController"/> implementation (a <see cref="DefaultEngineController{TMessage}"/>
    /// subclass) — the engine has no message DTO of its own, so a host must always supply one. Registered as
    /// <see cref="IEngineController"/> automatically; requiring this type parameter makes that requirement a
    /// compile-time error instead of a DI-resolution failure at startup.
    /// </typeparam>
    /// <param name="args">Command-line arguments passed from the host entry point.</param>
    /// <param name="windowIconUri">Optional <c>avares://</c> URI of the window icon; omit to use the OS default.</param>
    /// <param name="configureServices">Optional callback to register additional host-specific control-interface implementations.</param>
    public static async Task Start<TEngineController>(
        string[] args,
        Uri? windowIconUri = null,
        Action<IServiceCollection>? configureServices = null)
        where TEngineController : class, IEngineController
    {
        ConfigureServices = services =>
        {
            services.AddSingleton<IEngineController, TEngineController>();
            configureServices?.Invoke(services);
        };
        WindowIconUri = windowIconUri;

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
        ConfigureServices?.Invoke(services);
        services.TryAddSingleton<ICurrentUserProvider, CurrentUserProvider>();
        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IEngineController>();
    }

    private static async Task RunHeadless()
        => await Host.CreateDefaultBuilder()
            .UseEngineConfig(Config)
            .UseEngine(EngineMode.Headless)
            .ConfigureServices((_, services) => ConfigureServices?.Invoke(services))
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
