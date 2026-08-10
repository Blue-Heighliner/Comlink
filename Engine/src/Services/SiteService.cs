namespace BlueHeighliner.Comlink.Engine.Services;

/// <summary>Manages the local site identity: loading persisted state, applying debug overrides, and installing a new site.</summary>
public interface ISiteService
{
    /// <summary>Gets the currently loaded site state.</summary>
    SiteState CurrentState { get; }
    /// <summary>Returns a <see cref="SiteInfo"/> for the current site, or <see langword="null"/> if no site is installed.</summary>
    SiteInfo? GetCurrentSiteInfo();
    /// <summary>Loads the persisted site state from disk, or applies a debug override if one is registered.</summary>
    Task Load(CancellationToken cancellation = default);
    /// <summary>Resolves <paramref name="siteCode"/>, updates the local state, and persists it to disk.</summary>
    Task<SiteInfo?> Install(string siteCode, CancellationToken cancellation = default);
}

/// <summary>Manages the local site identity: loading persisted state, applying debug overrides, and installing a new site.</summary>
public sealed class SiteService : ISiteService
{
    private readonly ISiteCodeResolver _resolver;
    private readonly IEnumerable<IDebugSiteOverride> _debugOverrides;
    private readonly IAppDataPathProvider _appDataPathProvider;
    private readonly ICurrentSiteProvider _currentSiteProvider;
    private readonly ILogger _logger;
    private SiteState _state = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Initializes a new <see cref="SiteService"/> with the required infrastructure dependencies.</summary>
    public SiteService(
        ISiteCodeResolver resolver,
        IEnumerable<IDebugSiteOverride> debugOverrides,
        IAppDataPathProvider appDataPathProvider,
        ICurrentSiteProvider currentSiteProvider,
        ILoggerFactory loggerFactory)
    {
        _resolver = resolver;
        _debugOverrides = debugOverrides;
        _appDataPathProvider = appDataPathProvider;
        _currentSiteProvider = currentSiteProvider;
        _logger = loggerFactory.CreateLogger("APP");
    }

    private string StateFilePath => Path.Combine(_appDataPathProvider.AppDataPath, "State.json");

    /// <summary>Gets the currently loaded site state.</summary>
    public SiteState CurrentState => _state;

    /// <summary>Returns a <see cref="SiteInfo"/> for the current site, or <see langword="null"/> if no site is installed.</summary>
    public SiteInfo? GetCurrentSiteInfo()
    {
        if (!_state.IsInstalled) return null;
        return new SiteInfo
        {
            Name = _state.SiteName!,
            Code = _state.SiteCode!,
            EnvironmentTitle = _state.EnvironmentTitle!,
            EnvironmentColor = _state.EnvironmentColor!
        };
    }

    /// <summary>Loads the persisted site state from disk, or applies a debug override if one is registered.</summary>
    public async Task Load(CancellationToken cancellation = default)
    {
        string? debugSiteName = _debugOverrides.Select(o => o.SiteName).FirstOrDefault(n => n is not null);
        if (debugSiteName is not null)
        {
            string name = debugSiteName.ToUpperInvariant();
            _state = new SiteState
            {
                SiteName = name,
                SiteCode = name,
                EnvironmentTitle = "DEBUG",
                EnvironmentColor = "#FF6200"
            };
            _currentSiteProvider.SiteName = name;
            return;
        }

        string stateFilePath = StateFilePath;
        if (!File.Exists(stateFilePath)) return;

        try
        {
            string json = await File.ReadAllTextAsync(stateFilePath, cancellation).ConfigureAwait(false);
            _state = JsonSerializer.Deserialize<SiteState>(json) ?? new SiteState();
            if (_state.IsInstalled)
                _currentSiteProvider.SiteName = _state.SiteName;
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to load site state"); }
    }

    /// <summary>Resolves <paramref name="siteCode"/>, updates the local state, and persists it to disk.</summary>
    public async Task<SiteInfo?> Install(string siteCode, CancellationToken cancellation = default)
    {
        await _lock.WaitAsync(cancellation);
        try
        {
            SiteInfo? siteInfo = await _resolver.Resolve(siteCode, cancellation);
            if (siteInfo is null) return null;

            _state = new SiteState
            {
                SiteName = siteInfo.Name,
                SiteCode = siteInfo.Code,
                EnvironmentTitle = siteInfo.EnvironmentTitle,
                EnvironmentColor = siteInfo.EnvironmentColor
            };

            _currentSiteProvider.SiteName = siteInfo.Name;

            string stateFilePath = StateFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(stateFilePath)!);
            await File.WriteAllTextAsync(stateFilePath, JsonSerializer.Serialize(_state, JsonOptions), cancellation);
            return siteInfo;
        }
        finally
        {
            _lock.Release();
        }
    }
}
