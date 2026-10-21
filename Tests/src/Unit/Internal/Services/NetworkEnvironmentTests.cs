namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="NetworkEnvironment"/>.</summary>
public sealed class NetworkEnvironmentTests
{
    private static NetworkEnvironment Build(Mock<TestEngineController> controller, IUserCatalog? users = null)
        => new(Mock.Of<IServiceProvider>(), controller.Object, users ?? Mock.Of<IUserCatalog>(), Mock.Of<IEntryService>(), Mock.Of<IMessageEvents>(), Mock.Of<IMessageStorageService>(), Mock.Of<IAutoForwardTargetsRepository>(), Mock.Of<INetworkIndicator>(), LoggerFactory.Create(_ => { }));

    /// <summary>A user is at least a level when their own level ranks at or above it, and a user with no level of their own runs at the lowest.</summary>
    [Fact]
    public void IsAtLeast_ComparesTheUsersLevelWithTheGivenOne()
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.GetUserMessageLevel("ALICE")).Returns("SECRET");
        controller.Setup(c => c.GetUserMessageLevel("BOB")).Returns("INTERNAL");
        NetworkEnvironment environment = Build(controller);

        Assert.True(environment.IsAtLeast("ALICE", TestLevel.Secret));
        Assert.True(environment.IsAtLeast("BOB", TestLevel.Internal));
        Assert.False(environment.IsAtLeast("BOB", TestLevel.Secret));
        Assert.False(environment.IsAtLeast("NOBODY", TestLevel.Internal));
        Assert.True(environment.IsAtLeast("NOBODY", TestLevel.Public));
    }

    private static NetworkEnvironment BuildWithGroups(Mock<TestEngineController> controller)
    {
        Mock<IUserCatalog> catalog = new();
        catalog.SetupGet(c => c.Groups).Returns(new Dictionary<string, GroupInfo>
        {
            ["OPS"] = new() { Id = "OPS", Members = ["ALICE", "BOB"] },
            ["ALL"] = new() { Id = "ALL", Members = ["BOB", "CAROL", "ALICE"] }
        });
        return Build(controller, catalog.Object);
    }

    /// <summary>Groups are replaced by their members and every other target is a user, with no user twice.</summary>
    [Fact]
    public void GetDestinations_ExpandsGroups_AndDropsDuplicates()
    {
        NetworkEnvironment environment = BuildWithGroups(new Mock<TestEngineController>());

        IReadOnlySet<string> destinations = environment.GetDestinations(null, ["DAVE", "OPS", "ALL", "BOB"], out IReadOnlySet<string> excluded);

        Assert.True(destinations.SetEquals(["DAVE", "ALICE", "BOB", "CAROL"]));
        Assert.Empty(excluded);
    }

    /// <summary>Users whose message level ranks below the minimum are left out and reported.</summary>
    [Fact]
    public void GetDestinations_LeavesOutUsersBelowTheMinimumLevel_AndReportsThem()
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.GetUserMessageLevel("ALICE")).Returns("SECRET");
        controller.Setup(c => c.GetUserMessageLevel("BOB")).Returns("INTERNAL");
        NetworkEnvironment environment = BuildWithGroups(controller);

        IReadOnlySet<string> destinations = environment.GetDestinations(TestLevel.Secret, ["OPS"], out IReadOnlySet<string> excluded);

        Assert.True(destinations.SetEquals(["ALICE"]));
        Assert.True(excluded.SetEquals(["BOB"]));
    }

    /// <summary>A user is the one the catalog lists, or only a name for anyone else, such as an external system.</summary>
    [Fact]
    public void GetUser_IsTheListedUserInfo_OrJustAName()
    {
        Mock<IUserCatalog> catalog = new();
        catalog.SetupGet(c => c.Users).Returns(new Dictionary<string, UserInfo> { ["ALICE"] = new UserInfo { Name = "ALICE", Role = UserRole.Server } });
        NetworkEnvironment environment = Build(new Mock<TestEngineController>(), catalog.Object);

        Assert.Equal(UserRole.Server, environment.GetUser("ALICE").Role);
        Assert.Equal("FEED", environment.GetUser("FEED").Name);
        Assert.Null(environment.GetUser("FEED").Role);
    }
}
