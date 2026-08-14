namespace BlueHeighliner.Comlink.Engine;

/// <summary>Hosted service that starts the peer listener and the interface listener.</summary>
[ExcludeFromCodeCoverage]
internal sealed class EngineHost : IHostedService
{
    private readonly IUserService _userService;
    private readonly IPeerService _peerService;
    private readonly IInterfaceService _interfaceService;
    private readonly ILogger _logger;
    private readonly string _displayName;
    private CancellationTokenSource? _cts;

    /// <summary>Initializes a new <see cref="EngineHost"/> with required engine services.</summary>
    public EngineHost(
        IUserService userService,
        IPeerService peerService,
        IInterfaceService interfaceService,
        IAppSettings appSettings,
        EngineMode mode,
        ILoggerFactory loggerFactory)
    {
        _userService = userService;
        _peerService = peerService;
        _interfaceService = interfaceService;
        _logger = loggerFactory.CreateLogger("APP");
        _displayName = mode == EngineMode.Headless ? $"{appSettings.AppName} (Headless)" : appSettings.AppName;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{AppName} starting...", _displayName);
        await _userService.Load(cancellationToken);

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
