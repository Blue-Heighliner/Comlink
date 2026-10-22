namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="UserDirectory"/>.</summary>
public sealed class UserDirectoryTests
{
    private readonly FakePeerService peers = new();
    private readonly Mock<IUserService> users = new();

    private UserDirectory Build()
    {
        NetworkConfig network = new()
        {
            Users = new Dictionary<string, NetworkUserConfig>
            {
                ["SERVER2"] = new() { Role = "Server", Children = [new NetworkLinkConfig { User = "C1" }] },
                ["C1"] = new() { Role = "Client" },
                ["C2"] = new() { Role = "Client" }
            },
            UserGroups = new Dictionary<string, List<string>> { ["INNER"] = ["C1"], ["OUTER"] = ["INNER", "C2"] }
        };
        ServiceCollection services = new();
        services.AddSingleton<IPeerService>(peers);
        return new UserDirectory(services.BuildServiceProvider(), new TestEngineController(network), users.Object);
    }

    /// <summary>Each user the catalog lists carries everything known about them, such as their role, and groups are not users.</summary>
    [Fact]
    public void Users_CarryWhatIsKnownAboutEachUser_AndLeaveOutGroups()
    {
        UserDirectory catalog = Build();

        Assert.Equal(["C1", "C2", "SERVER2"], catalog.Users.Keys.Order());
        Assert.Equal(UserRole.Server, catalog.Users["SERVER2"].Role);
        Assert.Equal(["C1"], catalog.Users["server2"].Children.Select(link => link.User));
    }

    /// <summary>Only the users with a live connection are connected, and the answer follows the connections as they come and go.</summary>
    [Fact]
    public void ConnectedUsers_AreTheOnesWithALiveConnection_AtTheMomentOfAsking()
    {
        UserDirectory catalog = Build();
        Assert.Empty(catalog.ConnectedUsers);

        peers.Connected.Add("C1");

        Assert.Equal("C1", Assert.Single(catalog.ConnectedUsers).Key);
        Assert.Equal(UserRole.Client, catalog.ConnectedUsers["c1"].Role);
    }

    /// <summary>Each group is listed by id with its members expanded to users, groups within it included, and a user lists the groups it is directly in.</summary>
    [Fact]
    public void Groups_ListEachGroupWithItsExpandedMembers()
    {
        UserDirectory catalog = Build();

        Assert.Equal(["INNER", "OUTER"], catalog.Groups.Keys.Order());
        Assert.Equal("OUTER", catalog.Groups["outer"].Id);
        Assert.Equal(["C1", "C2"], catalog.Groups["OUTER"].Members.Order());
        Assert.Equal(["C1"], catalog.Groups["INNER"].Members);
        Assert.Equal(["INNER"], catalog.Users["C1"].Groups);
    }

    /// <summary>The current user is whoever the user service says, and without one the catalog says so instead of returning nothing.</summary>
    [Fact]
    public void CurrentUser_IsTheUserServicesUser_AndThrowsWithoutOne()
    {
        UserDirectory catalog = Build();
        users.Setup(u => u.GetCurrentUserInfo()).Returns(new UserInformation { Name = "ME" });
        Assert.Equal("ME", catalog.CurrentUser.Name);

        users.Setup(u => u.GetCurrentUserInfo()).Returns((UserInformation?)null);
        Assert.Throws<InvalidOperationException>(() => catalog.CurrentUser);
    }
}
