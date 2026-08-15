namespace BlueHeighliner.Comlink.Engine;

/// <summary>Avalonia <see cref="Application"/> subclass that bootstraps the DI host and main window.</summary>
[ExcludeFromCodeCoverage]
public partial class EngineApp : Application
{
    private IHost? host;

    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        host = Host.CreateDefaultBuilder()
            .UseEngineConfig(EngineApplication.Config)
            .UseEngine(EngineMode.Client)
            .UseEngineUi()
            .ConfigureServices((_, services) => EngineApplication.ConfigureServices?.Invoke(services))
            .UseEngineConfigOverrides()
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Information))
            .Build();

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Exception? ex = e.ExceptionObject as Exception;
            try
            {
                host.Services.GetService<ILoggerFactory>()
                    ?.CreateLogger("ACTIVITY")
                    ?.LogCritical(ex, "Unhandled exception: {Message}", ex?.Message ?? "Unknown error");
            }
            catch { }
        };

        // Run startup on thread pool to avoid SynchronizationContext deadlock with async continuations
        Task.Run(() => host.StartAsync(CancellationToken.None)).GetAwaiter().GetResult();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindow mainWindow = host.Services.GetRequiredService<MainWindow>();
            if (EngineApplication.WindowIconUri is { } iconUri)
            {
                mainWindow.Icon = new WindowIcon(AssetLoader.Open(iconUri));
            }
            desktop.MainWindow = mainWindow;
            desktop.Exit += async (_, _) => await host.StopAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
