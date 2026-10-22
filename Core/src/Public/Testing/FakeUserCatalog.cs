namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="IUserCatalog"/> service that answers the users, groups and connections it is given, which can be changed afterwards.</summary>
public sealed class FakeUserCatalog : IUserCatalog
{
    /// <summary>Initializes the catalog.</summary>
    /// <param name="currentUser">The user the node runs as.</param>
    /// <param name="users">Every user the network lists, found by name whatever the case, or <see langword="null"/> for only <paramref name="currentUser"/>.</param>
    /// <param name="groups">Every group, found by id whatever the case, or <see langword="null"/> for none.</param>
    public FakeUserCatalog(UserInfo currentUser, IEnumerable<UserInfo>? users = null, IEnumerable<GroupInfo>? groups = null)
    {
        CurrentUser = currentUser;
        Users = (users ?? [currentUser]).ToDictionary(user => user.Name, StringComparer.OrdinalIgnoreCase);
        Groups = (groups ?? []).ToDictionary(group => group.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public UserInfo CurrentUser { get; set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, UserInfo> Users { get; set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, UserInfo> ConnectedUsers { get; set; } = new Dictionary<string, UserInfo>(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GroupInfo> Groups { get; set; }
}
