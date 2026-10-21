namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="IUserCatalog" />
/// <param name="services">Resolves the peer service on demand, since the peer service is built from the transport that may ask for the catalog.</param>
/// <param name="engineController">Supplies the user directory and what is known about each user.</param>
/// <param name="userService">Supplies the user the node runs as.</param>
internal sealed class UserCatalog(IServiceProvider services, IEngineController engineController, IUserService userService) : IUserCatalog
{
    /// <inheritdoc />
    public UserInfo CurrentUser => userService.GetCurrentUserInfo() ?? throw new InvalidOperationException("The node has no current user, which only happens while its certificate cannot be used.");

    /// <inheritdoc />
    public IReadOnlyDictionary<string, UserInfo> Users => engineController.UserNames.ToDictionary(name => name, engineController.GetUserInfo, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, UserInfo> ConnectedUsers
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
