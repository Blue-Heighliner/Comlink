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
    /// Loads configuration from <paramref name="args"/>, registers <typeparamref name="TMessageFormat"/> as
    /// the engine's <see cref="IMessageFormat"/>, then starts the Engine in Headless mode or GUI mode.
    /// </summary>
    /// <typeparam name="TMessageFormat">
    /// The host's <see cref="IMessageFormat"/> implementation. The engine has no message DTO of its
    /// own, so a host must always supply one — requiring this type parameter makes that requirement a
    /// compile-time error instead of a DI-resolution failure at startup.
    /// </typeparam>
    /// <param name="args">Command-line arguments passed from the host entry point.</param>
    /// <param name="configureServices">Callback to register host-specific control-interface implementations.</param>
    /// <param name="windowIconUri">Optional <c>avares://</c> URI of the window icon; omit to use the OS default.</param>
    public static async Task Start<TMessageFormat>(
        string[] args,
        Action<IServiceCollection> configureServices,
        Uri? windowIconUri = null)
        where TMessageFormat : class, IMessageFormat
    {
        Config = EngineConfig.Load(args);
        ConfigureServices = services =>
        {
            services.AddSingleton<IMessageFormat, TMessageFormat>();
            configureServices(services);
        };
        WindowIconUri = windowIconUri;

        if (Config.HeadlessMode)
            await RunHeadless();
        else
            RunGui(args);
    }

    private static async Task RunHeadless()
    {
        IHost host = Host.CreateDefaultBuilder()
            .UseEngineConfig(Config)
            .UseEngine(EngineMode.Headless)
            .ConfigureServices((_, services) => ConfigureServices?.Invoke(services))
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Information))
            .Build();
        await host.RunAsync();
    }

    private static void RunGui(string[] args)
    {
        AppBuilder.Configure<EngineApp>()
            .UsePlatformDetect()
            // Render popups (e.g. the fill-in options dropdown) inside the owning window's own
            // surface instead of as separate X11 windows — avoids a class of Avalonia-on-X11 bugs
            // where a popup's GPU surface gets stuck and stops repainting until the process restarts.
            .With(new X11PlatformOptions { OverlayPopups = true })
            .UseEngineStyles()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}
