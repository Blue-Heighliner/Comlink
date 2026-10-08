namespace BlueHeighliner.Comlink;

/// <summary>Manages the local user identity: loading persisted state, applying debug overrides, and installing a new user.</summary>
internal interface IUserService
{
    /// <summary>Raised after <see cref="Install"/> registers a user, once the user is current and its state is saved.</summary>
    event Action? Installed;
    /// <summary>Raised after <see cref="Refresh"/> changed who the current user is, including to nobody, once the new user is current.</summary>
    event Action? Changed;
    /// <summary>Gets the currently loaded user state.</summary>
    UserState CurrentState { get; }
    /// <summary>Returns a <see cref="UserInfo"/> for the current user, or <see langword="null"/> if no user is installed.</summary>
    UserInfo? GetCurrentUserInfo();
    /// <summary>
    /// Loads the user named by the <c>--user</c> override if one is given, or else the installed user remembered in <c>User.json</c>. Either must be a user of the network whose certificate is in order
    /// (see <see cref="IEngineController.GetCertificateProblem"/>), and when it is not nobody is installed, so the install screen is shown; a remembered user that fails is also uninstalled, by deleting <c>User.json</c>.
    /// </summary>
    Task Load(CancellationToken cancellation = default);
    /// <summary>
    /// Reads <c>User.json</c> again while the engine runs. A file that names another user than the current one makes that user current once it passes the checks of <see cref="Load"/>, one that fails them is uninstalled
    /// (the file is deleted) like a remembered user that fails at startup, and a file that is gone uninstalls the current user. Does nothing when the user is fixed by <c>--user</c> or the file still names the current user.
    /// </summary>
    /// <returns><see langword="true"/> when the current user changed, was installed or was uninstalled, which <see cref="Changed"/> has then announced.</returns>
    Task<bool> Refresh(CancellationToken cancellation = default);
    /// <summary>Installs the user named <paramref name="userName"/>, updates the local state, and persists it to disk.</summary>
    /// <returns>The installed user, or <see langword="null"/> when the network has no user of that name.</returns>
    /// <exception cref="InvalidOperationException">The user's certificate is missing, is not issued to them or is not signed by the authority certificate; nothing is installed.</exception>
    Task<UserInfo?> Install(string userName, CancellationToken cancellation = default);
}

/// <summary>Manages the local user identity: loading persisted state, applying debug overrides, and installing a new user.</summary>
internal sealed class UserService : IUserService
{
    /// <summary>Initializes a new <see cref="UserService"/> with the required infrastructure dependencies.</summary>
    public UserService(
        IEngineController engineController,
        ICurrentUserProvider currentUserProvider,
        ILoggerFactory loggerFactory)
    {
        this.engineController = engineController;
        this.currentUserProvider = currentUserProvider;
        logger = loggerFactory.CreateLogger(LogCategories.App);
    }

    private readonly IEngineController engineController;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly ILogger logger;
    private readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
    private UserState state = new();
    private readonly SemaphoreSlim lockObject = new(1, 1);
    private string UserFilePath => engineController.UserFilePath;

    /// <inheritdoc />
    public event Action? Installed;

    /// <inheritdoc />
    public event Action? Changed;

    /// <summary>Gets the currently loaded user state.</summary>
    public UserState CurrentState => state;

    /// <summary>Returns a <see cref="UserInfo"/> for the current user, or <see langword="null"/> if no user is installed.</summary>
    public UserInfo? GetCurrentUserInfo()
    {
        if (!state.IsInstalled)
        {
            return null;
        }
        return engineController.GetUserInfo(state.UserName!);
    }

    /// <summary>Loads the user named by <c>--user</c>, or else the persisted installed user, after checking them; one that fails the check is not installed.</summary>
    public async Task Load(CancellationToken cancellation = default)
    {
        if (engineController.DebugUserName is { } requested)
        {
            if (Accept(requested) is { } name)
            {
                state = new UserState { UserName = name };
                currentUserProvider.UserName = name;
            }

            return;
        }

        string userFilePath = UserFilePath;
        if (!File.Exists(userFilePath))
        {
            return;
        }

        try
        {
            string json = await File.ReadAllTextAsync(userFilePath, cancellation).ConfigureAwait(false);
            state = JsonSerializer.Deserialize<UserState>(json) ?? new UserState();
            if (!state.IsInstalled)
            {
                return;
            }

            if (Accept(state.UserName!) is { } name)
            {
                state.UserName = name;
                currentUserProvider.UserName = name;
            }
            else
            {
                state = new UserState();
                File.Delete(userFilePath);
            }
        }
        catch (Exception ex)
        {
            logger.Record(LogEvents.LoadUserStateFailed, ex, "Failed to load user state");
            logger.Record(LogEvents.UserSettingsUnreadable, "The saved settings for the last user could not be read");
        }
    }

    /// <inheritdoc />
    public async Task<bool> Refresh(CancellationToken cancellation = default)
    {
        if (engineController.DebugUserName is not null)
        {
            return false;
        }

        bool changed = false;
        await lockObject.WaitAsync(cancellation);
        try
        {
            string? current = state.IsInstalled ? state.UserName : null;
            string? remembered = null;
            string userFilePath = UserFilePath;
            if (File.Exists(userFilePath))
            {
                try
                {
                    UserState read = JsonSerializer.Deserialize<UserState>(await File.ReadAllTextAsync(userFilePath, cancellation)) ?? new UserState();
                    remembered = read.IsInstalled ? read.UserName : null;
                }
                catch (Exception ex) when (ex is JsonException or IOException)
                {
                    logger.Record(LogEvents.LoadUserStateFailed, ex, "Failed to load user state");
                    logger.Record(LogEvents.UserSettingsUnreadable, "The saved settings for the last user could not be read");
                    return false;
                }
            }

            if (string.Equals(current, remembered, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string? accepted = remembered is null ? null : Accept(remembered);
            if (remembered is not null && accepted is null)
            {
                File.Delete(userFilePath);
            }
            if (string.Equals(current, accepted, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            state = accepted is null ? new UserState() : new UserState { UserName = accepted };
            currentUserProvider.UserName = accepted;
            changed = true;
        }
        finally
        {
            lockObject.Release();
        }

        if (changed)
        {
            Changed?.Invoke();
        }
        return changed;
    }

    // The checks every way of becoming the user goes through: the network lists the user and the user's certificate is in order.
    private string? Accept(string requested)
    {
        if (engineController.FindUserName(requested) is not { } name)
        {
            logger.Record(LogEvents.NotInstalled, "{UserName} is not installed: {Problem}", requested, "it is not a user of the network");
            return null;
        }

        if (engineController.GetCertificateProblem(name) is { } problem)
        {
            logger.Record(LogEvents.NotInstalled, "{UserName} is not installed: {Problem}", name, problem);
            return null;
        }

        return name;
    }

    /// <summary>Installs the user named <paramref name="userName"/>, updates the local state, and persists it to disk.</summary>
    public async Task<UserInfo?> Install(string userName, CancellationToken cancellation = default)
    {
        UserInfo? userInfo;
        await lockObject.WaitAsync(cancellation);
        try
        {
            string? name = engineController.FindUserName(userName);
            if (name is null)
            {
                return null;
            }
            if (engineController.GetCertificateProblem(name) is { } problem)
            {
                throw new InvalidOperationException(problem);
            }

            userInfo = engineController.GetUserInfo(name);
            state = new UserState { UserName = userInfo.Name };

            currentUserProvider.UserName = userInfo.Name;

            string userFilePath = UserFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(userFilePath)!);
            await File.WriteAllTextAsync(userFilePath, JsonSerializer.Serialize(state, jsonOptions), cancellation);
        }
        finally
        {
            lockObject.Release();
        }

        Installed?.Invoke();
        return userInfo;
    }
}
