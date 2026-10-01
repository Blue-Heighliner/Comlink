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
    /// <param name="userGroups">Every defined group as a map of group name to member names, for resolving each <see cref="Users"/> entry's direct memberships.</param>
    /// <param name="isConnected">Answers <see cref="IsConnected"/> for a user name.</param>
    public EngineContext(UserInfo currentUser, IReadOnlyList<string> userNames, IReadOnlyDictionary<string, IReadOnlyList<string>> userGroups, Func<string, bool> isConnected)
    {
        CurrentUser = currentUser;
        this.userNames = userNames;
        this.userGroups = userGroups;
        this.isConnected = isConnected;
    }

    private readonly IReadOnlyList<string> userNames;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> userGroups;
    private readonly Func<string, bool> isConnected;

    /// <inheritdoc />
    public UserInfo CurrentUser { get; }

    /// <inheritdoc />
    public IEnumerable<UserInfo> Users => userNames.Select(name => new UserInfo
    {
        Name = name,
        Groups = [.. userGroups.Where(group => group.Value.Contains(name, StringComparer.OrdinalIgnoreCase)).Select(group => group.Key)]
    });

    /// <inheritdoc />
    public IEnumerable<UserInfo> ConnectedUsers => Users.Where(user => IsConnected(user.Name));

    /// <inheritdoc />
    public bool IsConnected(string userName) => isConnected(userName);
}

/// <inheritdoc cref="IEngineContextFactory" />
/// <param name="services">Resolves the peer service on demand, since the peer service is built from the transport that asks for a context.</param>
/// <param name="engineController">Supplies the user directory and groups.</param>
/// <param name="userService">Supplies the installed user.</param>
internal sealed class EngineContextFactory(IServiceProvider services, IEngineController engineController, IUserService userService) : IEngineContextFactory
{
    /// <inheritdoc />
    public IEngineContext Create()
        => new EngineContext(
            userService.GetCurrentUserInfo() ?? throw new InvalidOperationException("A processor ran with no installed user, which should never happen: processors only run once one is installed."),
            engineController.Users,
            engineController.UserGroups,
            userName => services.GetRequiredService<IPeerService>().IsUserConnected(userName));
}
