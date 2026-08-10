namespace BlueHeighliner.Comlink.Engine;

/// <summary>Hosted service that starts the peer listener and the interface listener.</summary>
[ExcludeFromCodeCoverage]
internal sealed class EngineHost : IHostedService
{
    private readonly ISiteService _siteService;
    private readonly IPeerService _peerService;
    private readonly IInterfaceService _interfaceService;
    private readonly ILogger _logger;
    private readonly string _displayName;
    private CancellationTokenSource? _cts;

    /// <summary>Initializes a new <see cref="EngineHost"/> with required engine services.</summary>
    public EngineHost(
        ISiteService siteService,
        IPeerService peerService,
        IInterfaceService interfaceService,
        IAppNameProvider appNameProvider,
        EngineMode mode,
        ILoggerFactory loggerFactory)
    {
        _siteService = siteService;
        _peerService = peerService;
        _interfaceService = interfaceService;
        _logger = loggerFactory.CreateLogger("APP");
        _displayName = mode == EngineMode.Headless ? $"{appNameProvider.AppName} (Headless)" : appNameProvider.AppName;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{AppName} starting...", _displayName);
        await _siteService.Load(cancellationToken);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken cancellation = _cts.Token;

        _ = Task.Run(() => _peerService.Start(cancellation), cancellation);
        _ = Task.Run(() => _interfaceService.Start(cancellation), cancellation);

        _logger.LogInformation("{AppName} started", _displayName);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }
}
