namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="IUserCatalog{TProfile}"/> service that answers the users, groups and connections it is given, which can be changed afterwards.</summary>
/// <typeparam name="TProfile">The record type the host stated for user profiles in its <c>Types</c> call, or <see cref="NoProfile"/>.</typeparam>
public sealed class FakeUserCatalog<TProfile> : IUserCatalog<TProfile> where TProfile : class
{
    /// <summary>Initializes the catalog.</summary>
    /// <param name="currentUser">The user the node runs as.</param>
    /// <param name="users">Every user the network lists, found by name whatever the case, or <see langword="null"/> for only <paramref name="currentUser"/>.</param>
    /// <param name="groups">Every group, found by id whatever the case, or <see langword="null"/> for none.</param>
    public FakeUserCatalog(UserInfo<TProfile> currentUser, IEnumerable<UserInfo<TProfile>>? users = null, IEnumerable<GroupInfo>? groups = null)
    {
        CurrentUser = currentUser;
        Users = (users ?? [currentUser]).ToDictionary(user => user.Name, StringComparer.OrdinalIgnoreCase);
        Groups = (groups ?? []).ToDictionary(group => group.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public UserInfo<TProfile> CurrentUser { get; set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, UserInfo<TProfile>> Users { get; set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, UserInfo<TProfile>> ConnectedUsers { get; set; } = new Dictionary<string, UserInfo<TProfile>>(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GroupInfo> Groups { get; set; }
}
