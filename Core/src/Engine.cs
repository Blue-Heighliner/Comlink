namespace BlueHeighliner.Comlink;

/// <summary>Entry point that bootstraps the engine as a GUI application or a headless peer client.</summary>
[ExcludeFromCodeCoverage]
public static class Engine
{
    /// <summary>What the host stated in its <see cref="IEngineConfiguration"/>.</summary>
    internal static EngineBuilder Builder { get; private set; } = new();
    /// <summary>The network configuration loaded from the command-line arguments and the app data folder, when the host allows one.</summary>
    internal static NetworkConfig Network { get; private set; } = new();
    /// <summary>Records what Avalonia reports about itself once the engine's logging exists.</summary>
    internal static AvaloniaLogSink AvaloniaLog { get; } = new();
    /// <summary>What checking the node's certificates found when it started: who the node is, or why that cannot be told.</summary>
    internal static IdentityCheck Identity { get; private set; } = new() { Identity = new NodeIdentity { UserName = string.Empty, AuthorityFile = string.Empty, CertificateFile = string.Empty, CertificateThumbprint = string.Empty, AuthorityThumbprint = string.Empty } };
    /// <summary>The host's assemblies, whose classes are registered under the interface named for them (see <see cref="Start{TConfiguration}"/>).</summary>
    internal static IReadOnlyList<Assembly> HostAssemblies { get; private set; } = [];
    /// <summary>Registers the host's own services, applied to the running engine's container as well as the one its configuration is built in.</summary>
    internal static Action<IServiceCollection>? ConfigureServices { get; private set; }

    /// <summary>
    /// Constructs <typeparamref name="TConfiguration"/> through dependency injection and runs it, loads the configuration
    /// file if the configuration allows one and checks the certificates to work out who the node is, then starts the engine in Headless mode or GUI mode.
    /// </summary>
    /// <typeparam name="TConfiguration">
    /// Says how the engine runs; it must at least state the host's frame type. It is built from a container holding
    /// logging (<see cref="ILoggerFactory"/>, <see cref="ILogger{TCategoryName}"/>) plus whatever <paramref name="configureServices"/>
    /// registers, so its constructor may take any of those. That container is separate from the running engine's, which
    /// receives the same <paramref name="configureServices"/> registrations again, so a service registered there exists once
    /// in each.
    /// </typeparam>
    /// <param name="args">Command-line arguments passed from the host entry point.</param>
    /// <param name="configureServices">Registers the host's own services, which always take precedence over what the engine finds by convention (every concrete class of the configuration's assembly and of the entry assembly is registered as its <c>IThing</c> interface, so <c>MyService</c> answers for <c>IMyService</c> without being registered): the ones <typeparamref name="TConfiguration"/> depends on, and any others the host wants in the running engine (for example a hosted service that uses <see cref="IServiceConnection{TPriority, TLevel, TAspect, TProfile}"/>).</param>
    /// <exception cref="InvalidOperationException"><typeparamref name="TConfiguration"/> cannot be constructed, or is incomplete or contradicts itself.</exception>
    public static async Task Start<TConfiguration>(string[] args, Action<IServiceCollection>? configureServices = null) where TConfiguration : class, IEngineConfiguration
    {
        ConfigureServices = configureServices;
        HostAssemblies = [.. new[] { typeof(TConfiguration).Assembly, Assembly.GetEntryAssembly() }.OfType<Assembly>().Distinct()];
        Builder = EngineBuilder.Build<TConfiguration>(configureServices, HostAssemblies);
        await using (Builder)
        {
            string[] arguments = Builder.AreCommandLineOverridesAllowed ? args : [];
            Builder.DataFolderOverride = NetworkConfig.Argument(arguments, "--data") is { } data ? Path.GetFullPath(data) : null;
            Network = NetworkConfig.Load(arguments, Builder.AppDataFolder, Builder.UserProfileType, [.. Builder.LevelValues.Select(level => level.Key)]);
            Identity = MsmtCertificateLookup.CheckIdentity(Builder, Network);

            if (Identity.Problem is null && Network.Find(Identity.Identity.UserName)?.Headless == true)
            {
                await RunHeadless();
            }
            else
            {
                RunGui(args);
            }
        }
    }

    private static async Task RunHeadless()
        => await Host.CreateDefaultBuilder()
            .UseEngine(EngineMode.Headless, Builder, Network, Identity)
            .UseHostServices(ConfigureServices, HostAssemblies)
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
            .AfterSetup(_ => Avalonia.Logging.Logger.Sink = AvaloniaLog)
            .StartWithClassicDesktopLifetime(args);
}
