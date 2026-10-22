namespace BlueHeighliner.Comlink.Tests.Unit.Public.Models;

/// <summary>Unit tests for <see cref="UserInfo{TProfile}"/>.</summary>
public sealed class UserInfoTests
{
    private static UserInfo<object> Full() => new()
    {
        Name = "ALICE",
        MessageLevel = "HIGH",
        Groups = ["OPS"],
        Role = UserRole.Server,
        IpHost = "10.0.0.1",
        MsmtPort = 5,
        HdlcAddress = 3,
        HdlcPorts = ["SL0"],
        InterfacePort = 6,
        Parent = new UserLink { User = "ROOT" },
        Children = [new UserLink { User = "BOB" }],
        AutoForwarders = ["Alerts"],
        Profile = new TestProfile("4B", 2)
    };

    /// <summary>Converting to a typed info keeps every property, and types the profile; a typed info converts to itself, and a profile of another type becomes none.</summary>
    [Fact]
    public void From_KeepsEveryPropertyAndTypesTheProfile()
    {
        IUserInfo source = Full();

        UserInfo<TestProfile> typed = UserInfo<TestProfile>.From(source);

        foreach (System.Reflection.PropertyInfo property in typeof(IUserInfo).GetProperties().Where(property => property.Name is not nameof(IUserInfo.Profile)))
        {
            Assert.Equal(property.GetValue(source), property.GetValue(typed));
        }

        Assert.Equal(new TestProfile("4B", 2), typed.Profile);
        Assert.Same(typed, UserInfo<TestProfile>.From(typed));
        Assert.Null(UserInfo<NoProfile>.From(source).Profile);
        Assert.Null(UserInfo<TestProfile>.From(new UserInfo<object> { Name = "BOB" }).Profile);
    }
}
