namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="IUserCatalog{TLevel, TProfile}" />
/// <param name="directory">Supplies who is on the network.</param>
internal sealed class UserCatalog<TLevel, TProfile>(IUserDirectory directory) : IUserCatalog<TLevel, TProfile> where TLevel : struct, Enum where TProfile : class
{
    /// <inheritdoc />
    public UserInfo<TLevel, TProfile> Current => UserInfo<TLevel, TProfile>.From(directory.CurrentUser);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, UserInfo<TLevel, TProfile>> All => Typed(directory.Users);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, UserInfo<TLevel, TProfile>> Connected => Typed(directory.ConnectedUsers);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GroupInfo> Groups => directory.Groups;

    private Dictionary<string, UserInfo<TLevel, TProfile>> Typed(IReadOnlyDictionary<string, IUserInfo> users)
        => users.ToDictionary(user => user.Key, user => UserInfo<TLevel, TProfile>.From(user.Value), StringComparer.OrdinalIgnoreCase);
}
