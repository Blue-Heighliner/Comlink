namespace BlueHeighliner.Comlink;

/// <summary>Creates the <see cref="IEngineContext"/> handed to a host's handlers, once a user is installed.</summary>
internal interface IEngineContextFactory
{
    /// <summary>Creates a snapshot of the engine as it is now.</summary>
    /// <exception cref="InvalidOperationException">No user is installed, which should never happen where a handler runs.</exception>
    IEngineContext Create();
}

/// <summary>Implements <see cref="IEngineContext"/> over what the engine knows.</summary>
internal sealed class EngineContext : IEngineContext
{
    /// <summary>Initializes a new <see cref="EngineContext"/>.</summary>
    /// <param name="currentUser">This instance's own installed user.</param>
    /// <param name="userNames">Every known user name in the messaging system.</param>
    /// <param name="getUserInfo">Returns everything known about a user, including their role and direct group memberships, for each <see cref="Users"/> entry.</param>
    /// <param name="isConnected">Says whether a user name is currently reachable, which decides <see cref="ConnectedUsers"/>.</param>
    /// <param name="getGroupMembers">Answers <see cref="GetGroupMembers"/> for a group name.</param>
    public EngineContext(UserInfo currentUser, IReadOnlyList<string> userNames, Func<string, UserInfo> getUserInfo, Func<string, bool> isConnected, Func<string, IReadOnlyList<string>> getGroupMembers)
    {
        this.getGroupMembers = getGroupMembers;
        CurrentUser = currentUser;
        users = new(() => userNames.ToDictionary(name => name, getUserInfo));
        connectedUsers = new(() => Users.Where(user => isConnected(user.Key)).ToDictionary(user => user.Key, user => user.Value));
    }

    private readonly Lazy<Dictionary<string, UserInfo>> users;
    private readonly Lazy<Dictionary<string, UserInfo>> connectedUsers;
    private readonly Func<string, IReadOnlyList<string>> getGroupMembers;

    /// <inheritdoc />
    public UserInfo CurrentUser { get; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, UserInfo> Users => users.Value;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, UserInfo> ConnectedUsers => connectedUsers.Value;

    /// <inheritdoc />
    public IReadOnlyList<string> GetGroupMembers(string groupName) => getGroupMembers(groupName);
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
            userService.GetCurrentUserInfo() ?? throw new InvalidOperationException("A handler ran with no installed user, which should never happen: handlers only run once one is installed."),
            engineController.Users,
            engineController.GetUserInfo,
            userName => services.GetRequiredService<IPeerService>().IsUserConnected(userName),
            engineController.GetGroupMembers);
}
