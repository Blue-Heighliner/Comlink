namespace BlueHeighliner.Comlink.Services;

/// <summary>Creates the <see cref="IEngineContext"/> handed to a host's processors, once a user is installed.</summary>
internal interface IEngineContextFactory
{
    /// <summary>Creates a snapshot of the engine as it is now.</summary>
    /// <exception cref="InvalidOperationException">No user is installed, which should never happen where a processor runs.</exception>
    IEngineContext Create();
}

/// <summary>Implements <see cref="IEngineContext"/> over what the engine knows.</summary>
internal sealed class EngineContext : IEngineContext
{
    /// <summary>Initializes a new <see cref="EngineContext"/>.</summary>
    /// <param name="currentUser">This instance's own installed user.</param>
    /// <param name="userNames">Every known user name in the messaging system.</param>
    /// <param name="getUserInfo">Returns everything known about a user, including their role and direct group memberships, for each <see cref="Users"/> entry.</param>
    /// <param name="isConnected">Answers <see cref="IsConnected"/> for a user name.</param>
    public EngineContext(UserInfo currentUser, IReadOnlyList<string> userNames, Func<string, UserInfo> getUserInfo, Func<string, bool> isConnected)
    {
        CurrentUser = currentUser;
        this.userNames = userNames;
        this.getUserInfo = getUserInfo;
        this.isConnected = isConnected;
    }

    private readonly IReadOnlyList<string> userNames;
    private readonly Func<string, UserInfo> getUserInfo;
    private readonly Func<string, bool> isConnected;

    /// <inheritdoc />
    public UserInfo CurrentUser { get; }

    /// <inheritdoc />
    public IEnumerable<UserInfo> Users => userNames.Select(getUserInfo);

    /// <inheritdoc />
    public IEnumerable<UserInfo> ConnectedUsers => Users.Where(user => IsConnected(user.Name));

    /// <inheritdoc />
    public bool IsConnected(string userName) => isConnected(userName);
}

/// <inheritdoc cref="IEngineContextFactory" />
/// <param name="services">Resolves the peer service on demand, since the peer service is built from the transport that asks for a context.</param>
/// <param name="engineController">Supplies the user directory and what is known about each user.</param>
/// <param name="userService">Supplies the installed user.</param>
internal sealed class EngineContextFactory(IServiceProvider services, IEngineController engineController, IUserService userService) : IEngineContextFactory
{
    /// <inheritdoc />
    public IEngineContext Create()
        => new EngineContext(
            userService.GetCurrentUserInfo() ?? throw new InvalidOperationException("A processor ran with no installed user, which should never happen: processors only run once one is installed."),
            engineController.Users,
            engineController.GetUserInfo,
            userName => services.GetRequiredService<IPeerService>().IsUserConnected(userName));
}
