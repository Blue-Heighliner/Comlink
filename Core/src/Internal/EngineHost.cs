namespace BlueHeighliner.Comlink;

/// <summary>Hosted service that starts the peer listener and the interface listener.</summary>
[ExcludeFromCodeCoverage]
internal sealed class EngineHost : IHostedService
{
    /// <summary>Initializes a new <see cref="EngineHost"/> with required engine services.</summary>
    public EngineHost(
        IUserService userService,
        IReconfigurationService reconfiguration,
        IPeerService peerService,
        IInterfaceService interfaceService,
        IExternalSystemsService externalSystemsService,
        IEngineHooksService engineHooksService,
        IDisconnectAlarmService disconnectAlarmService,
        IEngineController engineController,
        ILiteDbContext database,
        EngineMode mode,
        ILoggerFactory loggerFactory)
    {
        this.database = database;
        this.userService = userService;
        this.reconfiguration = reconfiguration;
        this.peerService = peerService;
        this.interfaceService = interfaceService;
        this.externalSystemsService = externalSystemsService;
        this.engineHooksService = engineHooksService;
        this.disconnectAlarmService = disconnectAlarmService;
        this.engineController = engineController;
        engineController.Validate();
        logger = loggerFactory.CreateLogger(LogCategories.App);
        displayName = mode is EngineMode.Headless ? $"{engineController.AppName} (Headless)" : engineController.AppName;
    }

    private readonly ILiteDbContext database;
    private readonly IEngineController engineController;
    private readonly IUserService userService;
    private readonly IReconfigurationService reconfiguration;
    private readonly IPeerService peerService;
    private readonly IInterfaceService interfaceService;
    private readonly IExternalSystemsService externalSystemsService;
    private readonly IEngineHooksService engineHooksService;
    private readonly IDisconnectAlarmService disconnectAlarmService;
    private readonly MicrosoftLogger logger;
    private readonly string displayName;
    private CancellationTokenSource? cts;
    private int stopped;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.Record(LogEvents.AppStarting, "{AppName} starting", displayName);
        if (userService.Problem is { } problem)
        {
            logger.Record(LogEvents.IdentityFailed, "Cannot identify the user: {Problem}", problem);
        }

        await reconfiguration.Run();

        cts = new CancellationTokenSource();
        userService.Changed += RestartNetworking;
        if (userService.GetCurrentUserInfo() is not null)
        {
            StartNetworking();
        }

        logger.Record(LogEvents.AppStarted, "{AppName} started", displayName);
    }

    // Every service that runs on a user's behalf (identity certificate, role, connections) ends with the token it was given and can be started again, so another user is a stop and a start, and nobody is a stop.
    private void RestartNetworking()
    {
        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        cts = new CancellationTokenSource();
        if (userService.GetCurrentUserInfo() is not null)
        {
            StartNetworking();
        }
    }

    private void StartNetworking()
    {
        database.Initialize();
        CancellationToken cancellation = cts!.Token;
        RunInBackground("Peer service", () => peerService.Start(cancellation), cancellation);
        RunInBackground("Interface service", () => interfaceService.Start(cancellation), cancellation);
        RunInBackground("External systems service", () => externalSystemsService.Start(cancellation), cancellation);
        RunInBackground("Engine hooks service", () => engineHooksService.Start(cancellation), cancellation);
        RunInBackground("Disconnect alarm service", () => disconnectAlarmService.Start(cancellation), cancellation);
    }

    // Each service runs until cancelled, so one that ends any other way has failed, and nothing else would ever say so.
    private void RunInBackground(string name, Func<Task> run, CancellationToken cancellation)
        => _ = Task.Run(async () =>
        {
            try { await run(); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.Record(LogEvents.ServiceStoppedUnexpectedly, ex, "{Service} stopped unexpectedly", name);
                logger.Record(LogEvents.ServiceStopped, "Part of the application stopped working unexpectedly");
            }
        }, cancellation);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref stopped, 1) != 0)
        {
            return Task.CompletedTask;
        }

        logger.Record(LogEvents.AppExited, "{AppName} exited", displayName);
        userService.Changed -= RestartNetworking;
        cts?.Cancel();
        return Task.CompletedTask;
    }
}
