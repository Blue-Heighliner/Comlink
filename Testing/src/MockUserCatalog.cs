namespace BlueHeighliner.Comlink.Testing;

/// <summary>A mock of the <see cref="IUserCatalog"/> a handler asks for. It starts with the users, groups and connections it is given and can be changed afterwards, so a test sets up who is on the network.</summary>
public sealed class MockUserCatalog : Mock<IUserCatalog>
{
    /// <summary>Initializes the mock.</summary>
    /// <param name="currentUser">The user the node runs as.</param>
    /// <param name="users">Every user of the network, which is also the users that are connected unless <see cref="ConnectedUsers"/> is set; <see langword="null"/> for just <paramref name="currentUser"/>.</param>
    /// <param name="groups">Every group of the network, or <see langword="null"/> for none.</param>
    public MockUserCatalog(UserInfo currentUser, IEnumerable<UserInfo>? users = null, IEnumerable<GroupInfo>? groups = null)
    {
        CurrentUser = currentUser;
        Users = (users ?? [currentUser]).ToDictionary(user => user.Name, StringComparer.OrdinalIgnoreCase);
        ConnectedUsers = new Dictionary<string, UserInfo>(StringComparer.OrdinalIgnoreCase);
        Groups = (groups ?? []).ToDictionary(group => group.Id, StringComparer.OrdinalIgnoreCase);
        Setup(catalog => catalog.CurrentUser).Returns(() => CurrentUser);
        Setup(catalog => catalog.Users).Returns(() => Users);
        Setup(catalog => catalog.ConnectedUsers).Returns(() => ConnectedUsers);
        Setup(catalog => catalog.Groups).Returns(() => Groups);
    }

    /// <summary>Gets or sets what <see cref="IUserCatalog.CurrentUser"/> returns.</summary>
    public UserInfo CurrentUser { get; set; }

    /// <summary>Gets or sets what <see cref="IUserCatalog.Users"/> returns, by name.</summary>
    public IReadOnlyDictionary<string, UserInfo> Users { get; set; }

    /// <summary>Gets or sets what <see cref="IUserCatalog.ConnectedUsers"/> returns, by name. Nobody is connected until a test says so.</summary>
    public IReadOnlyDictionary<string, UserInfo> ConnectedUsers { get; set; }

    /// <summary>Gets or sets what <see cref="IUserCatalog.Groups"/> returns, by id.</summary>
    public IReadOnlyDictionary<string, GroupInfo> Groups { get; set; }
}
