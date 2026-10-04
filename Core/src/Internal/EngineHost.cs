namespace BlueHeighliner.Comlink;

/// <summary>Hosted service that starts the peer listener and the interface listener.</summary>
[ExcludeFromCodeCoverage]
internal sealed class EngineHost : IHostedService
{
    /// <summary>Initializes a new <see cref="EngineHost"/> with required engine services.</summary>
    public EngineHost(
        IUserService userService,
        IPeerService peerService,
        IInterfaceService interfaceService,
        IExternalSystemsService externalSystemsService,
        IEngineHooksService engineHooksService,
        IAutoForwardService autoForwardService,
        IEngineController engineController,
        EngineMode mode,
        ILoggerFactory loggerFactory)
    {
        this.userService = userService;
        this.peerService = peerService;
        this.interfaceService = interfaceService;
        this.externalSystemsService = externalSystemsService;
        this.engineHooksService = engineHooksService;
        this.autoForwardService = autoForwardService;
        engineController.Validate();
        logger = loggerFactory.CreateLogger("APP");
        displayName = mode == EngineMode.Headless ? $"{engineController.AppName} (Headless)" : engineController.AppName;
    }

    private readonly IUserService userService;
    private readonly IPeerService peerService;
    private readonly IInterfaceService interfaceService;
    private readonly IExternalSystemsService externalSystemsService;
    private readonly IEngineHooksService engineHooksService;
    private readonly IAutoForwardService autoForwardService;
    private readonly ILogger logger;
    private readonly string displayName;
    private CancellationTokenSource? cts;
    private int networkingStarted;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{AppName} starting...", displayName);
        await userService.Load(cancellationToken);

        cts = new CancellationTokenSource();

        // Networking needs the current user (its identity certificate, and a Server's own place in the cluster), so on
        // a first run it waits for the install screen instead of starting without one and staying offline until the
        // next restart. Subscribing before checking means an install that lands in between is not missed.
        userService.Installed += StartNetworking;
        if (userService.GetCurrentUserInfo() is not null)
        {
            StartNetworking();
        }
        else
        {
            logger.LogInformation("{AppName} will connect once a user is installed", displayName);
        }

        logger.LogInformation("{AppName} started", displayName);
    }

    private void StartNetworking()
    {
        if (Interlocked.Exchange(ref networkingStarted, 1) != 0) { return; }
        userService.Installed -= StartNetworking;

        CancellationToken cancellation = cts!.Token;
        RunInBackground("Peer service", () => peerService.Start(cancellation), cancellation);
        RunInBackground("Interface service", () => interfaceService.Start(cancellation), cancellation);
        RunInBackground("External systems service", () => externalSystemsService.Start(cancellation), cancellation);
        RunInBackground("Engine hooks service", () => engineHooksService.Start(cancellation), cancellation);
        RunInBackground("Auto forward service", () => autoForwardService.Start(cancellation), cancellation);
    }

    // Each service runs until cancelled, so one that ends any other way has failed, and nothing else would ever say so.
    private void RunInBackground(string name, Func<Task> run, CancellationToken cancellation)
        => _ = Task.Run(async () =>
        {
            try { await run(); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogCritical(ex, "{Service} stopped unexpectedly", name); }
        }, cancellation);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        userService.Installed -= StartNetworking;
        cts?.Cancel();
        return Task.CompletedTask;
    }
}
