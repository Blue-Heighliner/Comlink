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
    /// <summary>Initializes a new <see cref="UserService"/> with the required infrastructure dependencies.</summary>
    public UserService(
        IEngineController engineController,
        ICurrentUserProvider currentUserProvider,
        ILoggerFactory loggerFactory)
    {
        this.engineController = engineController;
        this.currentUserProvider = currentUserProvider;
        logger = loggerFactory.CreateLogger("APP");
    }

    private readonly IEngineController engineController;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly ILogger logger;
    private readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
    private UserState state = new();
    private readonly SemaphoreSlim lockObject = new(1, 1);
    private string StateFilePath => Path.Combine(engineController.AppDataPath, "State.json");

    /// <summary>Gets the currently loaded user state.</summary>
    public UserState CurrentState => state;

    /// <summary>Returns a <see cref="UserInfo"/> for the current user, or <see langword="null"/> if no user is installed.</summary>
    public UserInfo? GetCurrentUserInfo()
    {
        if (!state.IsInstalled) { return null; }
        return new UserInfo
        {
            Name = state.UserName!,
            Code = state.UserCode!,
            EnvironmentTitle = state.EnvironmentTitle!,
            EnvironmentColor = state.EnvironmentColor!
        };
    }

    /// <summary>Loads the persisted user state from disk, or applies a debug override if one is registered.</summary>
    public async Task Load(CancellationToken cancellation = default)
    {
        string? debugUserName = engineController.DebugUserName;
        if (debugUserName is not null)
        {
            string name = debugUserName.ToUpperInvariant();
            state = new UserState
            {
                UserName = name,
                UserCode = name,
                EnvironmentTitle = "DEBUG",
                EnvironmentColor = "#FF6200"
            };
            currentUserProvider.UserName = name;
            return;
        }

        string stateFilePath = StateFilePath;
        if (!File.Exists(stateFilePath)) { return; }

        try
        {
            string json = await File.ReadAllTextAsync(stateFilePath, cancellation).ConfigureAwait(false);
            state = JsonSerializer.Deserialize<UserState>(json) ?? new UserState();
            if (state.IsInstalled)
            {
                currentUserProvider.UserName = state.UserName;
            }
        }
        catch (Exception ex) { logger.LogError(ex, "Failed to load user state"); }
    }

    /// <summary>Resolves <paramref name="userCode"/>, updates the local state, and persists it to disk.</summary>
    public async Task<UserInfo?> Install(string userCode, CancellationToken cancellation = default)
    {
        await lockObject.WaitAsync(cancellation);
        try
        {
            UserInfo? userInfo = engineController.ResolveCode(userCode);
            if (userInfo is null) { return null; }

            state = new UserState
            {
                UserName = userInfo.Name,
                UserCode = userInfo.Code,
                EnvironmentTitle = userInfo.EnvironmentTitle,
                EnvironmentColor = userInfo.EnvironmentColor
            };

            currentUserProvider.UserName = userInfo.Name;

            string stateFilePath = StateFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(stateFilePath)!);
            await File.WriteAllTextAsync(stateFilePath, JsonSerializer.Serialize(state, jsonOptions), cancellation);
            return userInfo;
        }
        finally
        {
            lockObject.Release();
        }
    }
}
