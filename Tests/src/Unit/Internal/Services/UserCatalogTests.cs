namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="UserCatalog{TProfile}"/>.</summary>
public sealed class UserCatalogTests
{
    private static UserInformation Alice() => new() { Name = "ALICE", Groups = ["OPS"], Profile = new TestProfile("4B", 2) };

    /// <summary>The users the directory knows reach the host typed by its profile type, current and connected ones included, and a profile of another type is none.</summary>
    [Fact]
    public void Users_AreTypedByTheHostsProfile()
    {
        Mock<IUserDirectory> directory = new();
        IUserInfo bob = new UserInformation { Name = "BOB", Profile = "not a TestProfile" };
        directory.SetupGet(d => d.CurrentUser).Returns(Alice());
        directory.SetupGet(d => d.Users).Returns(new Dictionary<string, IUserInfo> { ["ALICE"] = Alice(), ["BOB"] = bob });
        directory.SetupGet(d => d.ConnectedUsers).Returns(new Dictionary<string, IUserInfo> { ["ALICE"] = Alice() });
        directory.SetupGet(d => d.Groups).Returns(new Dictionary<string, GroupInfo> { ["OPS"] = new() { Id = "OPS", Members = ["ALICE"] } });
        UserCatalog<TestLevel, TestProfile> catalog = new(directory.Object);

        Assert.Equal(new TestProfile("4B", 2), catalog.CurrentUser.Profile);
        Assert.Equal(new TestProfile("4B", 2), catalog.Users["alice"].Profile);
        Assert.Null(catalog.Users["BOB"].Profile);
        Assert.Equal("ALICE", Assert.Single(catalog.ConnectedUsers).Value.Name);
        Assert.Equal(["ALICE"], catalog.Groups["OPS"].Members);
    }
}
