namespace BlueHeighliner.Comlink;

/// <summary>Avalonia <see cref="Application"/> subclass that bootstraps the DI host and main window.</summary>
[ExcludeFromCodeCoverage]
internal partial class EngineApp : Application
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
            .UseEngine(EngineMode.Client, Engine.Builder, Engine.Network)
            .UseEngineUi()
            .ConfigureServices((_, services) => Engine.ConfigureServices?.Invoke(services))
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
            if (host.Services.GetRequiredService<IEngineController>().WindowIconUri is { } iconUri)
            {
                mainWindow.Icon = new WindowIcon(AssetLoader.Open(iconUri));
            }
            desktop.MainWindow = mainWindow;

            // The host's console lifetime swallows SIGTERM and Ctrl+C, only signalling ApplicationStopping, so the
            // window has to be closed from here or the process would ignore the signal and keep running.
            bool exiting = false;
            host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(
                () => Dispatcher.UIThread.Post(() => { if (!exiting) { desktop.Shutdown(); } }));

            // Stopping and disposing the host is what releases serial ports, closes connections, and closes the
            // database. Exit is raised synchronously as the process shuts down, so an async handler would be cut off;
            // it runs on the thread pool to avoid deadlocking on the UI synchronization context, and is bounded so a
            // stuck service cannot hang the exit.
            desktop.Exit += (_, _) =>
            {
                exiting = true;
                Task.Run(() => Shutdown(host)).Wait(TimeSpan.FromSeconds(10));
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task Shutdown(IHost host)
    {
        try { await host.StopAsync(); }
        finally
        {
            if (host is IAsyncDisposable asyncDisposable) { await asyncDisposable.DisposeAsync(); }
            else { host.Dispose(); }
        }
    }
}
