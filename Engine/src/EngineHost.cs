namespace BlueHeighliner.Comlink.Engine;

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
        IEngineController engineController,
        EngineMode mode,
        ILoggerFactory loggerFactory)
    {
        this.userService = userService;
        this.peerService = peerService;
        this.interfaceService = interfaceService;
        this.externalSystemsService = externalSystemsService;
        logger = loggerFactory.CreateLogger("APP");
        displayName = mode == EngineMode.Headless ? $"{engineController.AppName} (Headless)" : engineController.AppName;
    }

    private readonly IUserService userService;
    private readonly IPeerService peerService;
    private readonly IInterfaceService interfaceService;
    private readonly IExternalSystemsService externalSystemsService;
    private readonly ILogger logger;
    private readonly string displayName;
    private CancellationTokenSource? cts;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{AppName} starting...", displayName);
        await userService.Load(cancellationToken);

        cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken cancellation = cts.Token;

        _ = Task.Run(() => peerService.Start(cancellation), cancellation);
        _ = Task.Run(() => interfaceService.Start(cancellation), cancellation);
        _ = Task.Run(() => externalSystemsService.Start(cancellation), cancellation);

        logger.LogInformation("{AppName} started", displayName);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        cts?.Cancel();
        return Task.CompletedTask;
    }
}
