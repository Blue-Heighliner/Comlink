namespace BlueHeighliner.Comlink;

/// <summary>Who is on the network, without the typing of the user profiles the host's <c>Types</c> call states: what the engine itself works with, and what <see cref="UserCatalog{TProfile}"/> hands to the host typed.</summary>
internal interface IUserDirectory
{
    /// <summary>Gets the user this node runs as.</summary>
    /// <exception cref="InvalidOperationException">The node has no current user.</exception>
    IUserInfo CurrentUser { get; }

    /// <summary>Gets every known user, by name.</summary>
    IReadOnlyDictionary<string, IUserInfo> Users { get; }

    /// <summary>Gets the users currently reachable over at least one live peer connection, by name.</summary>
    IReadOnlyDictionary<string, IUserInfo> ConnectedUsers { get; }

    /// <summary>Gets every group, by id.</summary>
    IReadOnlyDictionary<string, GroupInfo> Groups { get; }
}

/// <inheritdoc cref="IUserDirectory" />
/// <param name="services">Resolves the peer service on demand, since the peer service is built from the transport that may ask for the directory.</param>
/// <param name="engineController">Supplies the user directory and what is known about each user.</param>
/// <param name="userService">Supplies the user the node runs as.</param>
internal sealed class UserDirectory(IServiceProvider services, IEngineController engineController, IUserService userService) : IUserDirectory
{
    /// <inheritdoc />
    public IUserInfo CurrentUser => userService.GetCurrentUserInfo() ?? throw new InvalidOperationException("The node has no current user, which only happens while its certificate cannot be used.");

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IUserInfo> Users => engineController.UserNames.ToDictionary(name => name, engineController.GetUserInfo, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IUserInfo> ConnectedUsers
    {
        get
        {
            IPeerService peers = services.GetRequiredService<IPeerService>();
            return Users.Where(user => peers.IsUserConnected(user.Key)).ToDictionary(user => user.Key, user => user.Value, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GroupInfo> Groups => engineController.UserGroups.Keys.ToDictionary(id => id, id => new GroupInfo { Id = id, Members = engineController.GetGroupMembers(id) }, StringComparer.OrdinalIgnoreCase);
}
