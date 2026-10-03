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
            ["RELAY"] = new UserInfo { Name = "RELAY", Role = UserRole.Relay, Children = ["C1"] },
            ["C1"] = new UserInfo { Name = "C1", Role = UserRole.Client }
        };
        EngineContext context = new(new UserInfo { Name = "ME" }, ["RELAY", "C1"], name => known[name], name => name == "RELAY");

        Assert.Equal([UserRole.Relay, UserRole.Client], context.Users.Select(user => user.Role));
        Assert.Equal("RELAY", Assert.Single(context.ConnectedUsers).Name);
        Assert.Equal(["C1"], context.ConnectedUsers.Single().Children.Select(link => link.User));
    }
}
