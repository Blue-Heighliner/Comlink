namespace BlueHeighliner.Comlink.Engine.Services;

/// <summary>Manages the local user identity: loading persisted state, applying debug overrides, and installing a new user.</summary>
public interface IUserService
{
    /// <summary>Gets the currently loaded user state.</summary>
    UserState CurrentState { get; }
    /// <summary>Returns a <see cref="UserInfo"/> for the current user, or <see langword="null"/> if no user is installed.</summary>
    UserInfo? GetCurrentUserInfo();
    /// <summary>Loads the persisted user state from disk, or applies a debug override if one is registered.</summary>
    Task Load(CancellationToken cancellation = default);
    /// <summary>Resolves <paramref name="userCode"/>, updates the local state, and persists it to disk.</summary>
    Task<UserInfo?> Install(string userCode, CancellationToken cancellation = default);
}

/// <summary>Manages the local user identity: loading persisted state, applying debug overrides, and installing a new user.</summary>
public sealed class UserService : IUserService
{
    private readonly IUserIdentity _userIdentity;
    private readonly IAppSettings _appSettings;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger _logger;
    private UserState _state = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Initializes a new <see cref="UserService"/> with the required infrastructure dependencies.</summary>
    public UserService(
        IUserIdentity userIdentity,
        IAppSettings appSettings,
        ICurrentUserProvider currentUserProvider,
        ILoggerFactory loggerFactory)
    {
        _userIdentity = userIdentity;
        _appSettings = appSettings;
        _currentUserProvider = currentUserProvider;
        _logger = loggerFactory.CreateLogger("APP");
    }

    private string StateFilePath => Path.Combine(_appSettings.AppDataPath, "State.json");

    /// <summary>Gets the currently loaded user state.</summary>
    public UserState CurrentState => _state;

    /// <summary>Returns a <see cref="UserInfo"/> for the current user, or <see langword="null"/> if no user is installed.</summary>
    public UserInfo? GetCurrentUserInfo()
    {
        if (!_state.IsInstalled) return null;
        return new UserInfo
        {
            Name = _state.UserName!,
            Code = _state.UserCode!,
            EnvironmentTitle = _state.EnvironmentTitle!,
            EnvironmentColor = _state.EnvironmentColor!
        };
    }

    /// <summary>Loads the persisted user state from disk, or applies a debug override if one is registered.</summary>
    public async Task Load(CancellationToken cancellation = default)
    {
        string? debugUserName = _userIdentity.DebugUserName;
        if (debugUserName is not null)
        {
            string name = debugUserName.ToUpperInvariant();
            _state = new UserState
            {
                UserName = name,
                UserCode = name,
                EnvironmentTitle = "DEBUG",
                EnvironmentColor = "#FF6200"
            };
            _currentUserProvider.UserName = name;
            return;
        }

        string stateFilePath = StateFilePath;
        if (!File.Exists(stateFilePath)) return;

        try
        {
            string json = await File.ReadAllTextAsync(stateFilePath, cancellation).ConfigureAwait(false);
            _state = JsonSerializer.Deserialize<UserState>(json) ?? new UserState();
            if (_state.IsInstalled)
                _currentUserProvider.UserName = _state.UserName;
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to load user state"); }
    }

    /// <summary>Resolves <paramref name="userCode"/>, updates the local state, and persists it to disk.</summary>
    public async Task<UserInfo?> Install(string userCode, CancellationToken cancellation = default)
    {
        await _lock.WaitAsync(cancellation);
        try
        {
            UserInfo? userInfo = await _userIdentity.ResolveCode(userCode, cancellation);
            if (userInfo is null) return null;

            _state = new UserState
            {
                UserName = userInfo.Name,
                UserCode = userInfo.Code,
                EnvironmentTitle = userInfo.EnvironmentTitle,
                EnvironmentColor = userInfo.EnvironmentColor
            };

            _currentUserProvider.UserName = userInfo.Name;

            string stateFilePath = StateFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(stateFilePath)!);
            await File.WriteAllTextAsync(stateFilePath, JsonSerializer.Serialize(_state, JsonOptions), cancellation);
            return userInfo;
        }
        finally
        {
            _lock.Release();
        }
    }
}
