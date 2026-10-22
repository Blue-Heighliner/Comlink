namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="IUserCatalog{TProfile}" />
/// <param name="directory">Supplies who is on the network.</param>
internal sealed class UserCatalog<TProfile>(IUserDirectory directory) : IUserCatalog<TProfile> where TProfile : class
{
    /// <inheritdoc />
    public UserInfo<TProfile> CurrentUser => UserInfo<TProfile>.From(directory.CurrentUser);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, UserInfo<TProfile>> Users => Typed(directory.Users);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, UserInfo<TProfile>> ConnectedUsers => Typed(directory.ConnectedUsers);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GroupInfo> Groups => directory.Groups;

    private Dictionary<string, UserInfo<TProfile>> Typed(IReadOnlyDictionary<string, IUserInfo> users)
        => users.ToDictionary(user => user.Key, user => UserInfo<TProfile>.From(user.Value), StringComparer.OrdinalIgnoreCase);
}
