namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="EngineContext"/>.</summary>
public sealed class EngineContextTests
{
    /// <summary>Each user the context lists carries everything known about them, such as their role, not just a name.</summary>
    [Fact]
    public void Users_CarryWhatIsKnownAboutEachUser()
    {
        Dictionary<string, UserInfo> known = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SERVER2"] = new UserInfo { Name = "SERVER2", Role = UserRole.Server, Children = ["C1"] },
            ["C1"] = new UserInfo { Name = "C1", Role = UserRole.Client }
        };
        EngineContext context = new(new UserInfo { Name = "ME" }, ["SERVER2", "C1"], name => known[name], name => name == "SERVER2", _ => []);

        Assert.Equal([UserRole.Server, UserRole.Client], context.Users.Values.Select(user => user.Role));
        Assert.Equal("SERVER2", Assert.Single(context.ConnectedUsers).Key);
        Assert.Equal(["C1"], context.ConnectedUsers.Values.Single().Children.Select(link => link.User));
    }
}
