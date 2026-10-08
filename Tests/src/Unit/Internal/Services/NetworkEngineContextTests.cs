namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="NetworkEngineContext{TFrame, TPriority, TLevel, TAspect}"/>.</summary>
public sealed class NetworkEngineContextTests
{
    private static NetworkConnectedContext<TestFrame, TestMessagePriority, TestLevel, TestAspect> Build(Mock<INetworkEnvironment>? environment = null)
    {
        Mock<IEngineContext> engine = new();
        engine.SetupGet(e => e.Users).Returns(new Dictionary<string, UserInfo>());
        engine.Setup(e => e.GetGroupMembers(It.IsAny<string>())).Returns([]);
        engine.Setup(e => e.GetGroupMembers("OPS")).Returns(["ALICE", "BOB"]);
        engine.Setup(e => e.GetGroupMembers("ALL")).Returns(["BOB", "CAROL", "ALICE"]);
        environment ??= new();
        environment.Setup(e => e.CreateEngineContext()).Returns(engine.Object);
        return new NetworkConnectedContext<TestFrame, TestMessagePriority, TestLevel, TestAspect>(environment.Object, "BOB");
    }

    /// <summary>Groups are replaced by their members and every other target is a user, with no user twice and in the order first seen.</summary>
    [Fact]
    public void GetDestinations_ExpandsGroups_AndDropsDuplicates() => Assert.True(Build().GetDestinations(null, out _, "DAVE", "OPS", "ALL", "BOB").SetEquals(["DAVE", "ALICE", "BOB", "CAROL"]));

    /// <summary>A message is for the users of all its addresses except the external ones, with groups expanded.</summary>
    [Fact]
    public void GetDestinations_OfAMessage_LeavesOutExternalAddresses()
    {
        Message<TestMessagePriority, TestLevel, TestAspect> message = new()
        {
            Id = "M1",
            FromUser = "ALICE",
            Body = "Hi",
            Addresses =
            [
                new MessageAddress { UserName = "OPS", Type = AddressType.To },
                new MessageAddress { UserName = "ERIN", Type = AddressType.Cc },
                new MessageAddress { UserName = "OMAHA", Type = AddressType.External }
            ],
            SentAt = DateTime.UtcNow,
            Priority = TestMessagePriority.Normal
        };

        Assert.True(Build().GetDestinations(message, out _).SetEquals(["ALICE", "BOB", "ERIN"]));
    }

    /// <summary>Users whose message level ranks below the minimum are left out, and a message uses its own level as the minimum.</summary>
    [Fact]
    public void GetDestinations_LeavesOutUsersBelowTheMinimumLevel_AndReportsThem()
    {
        Mock<INetworkEnvironment> environment = new();
        environment.Setup(e => e.IsAtLeast(It.IsAny<string>(), TestLevel.Secret)).Returns(false);
        environment.Setup(e => e.IsAtLeast("ALICE", TestLevel.Secret)).Returns(true);
        NetworkConnectedContext<TestFrame, TestMessagePriority, TestLevel, TestAspect> context = Build(environment);
        Message<TestMessagePriority, TestLevel, TestAspect> message = new()
        {
            Id = "M1",
            FromUser = "ALICE",
            Body = "Hi",
            Addresses = [new MessageAddress { UserName = "OPS", Type = AddressType.To }],
            SentAt = DateTime.UtcNow,
            Priority = TestMessagePriority.Normal,
            MessageLevel = TestLevel.Secret
        };

        Assert.True(context.GetDestinations(TestLevel.Secret, out IReadOnlySet<string> excluded, "OPS").SetEquals(["ALICE"]));
        Assert.True(excluded.SetEquals(["BOB"]));
        Assert.True(context.GetDestinations(message, out excluded).SetEquals(["ALICE"]));
        Assert.True(excluded.SetEquals(["BOB"]));
        Assert.True(context.GetDestinations(null, out excluded, "OPS").SetEquals(["ALICE", "BOB"]));
        Assert.Empty(excluded);
    }

    /// <summary>Setting the sent status for several users sets it for each of them.</summary>
    [Fact]
    public async Task SetSentStatus_ForSeveralUsers_SetsEachOne()
    {
        Mock<INetworkEnvironment> environment = new();
        NetworkConnectedContext<TestFrame, TestMessagePriority, TestLevel, TestAspect> context = Build(environment);

        await context.SetSentStatus("M1", ["ALICE", "BOB"], DestinationStatus.Sent);

        environment.Verify(e => e.SetSentStatus("M1", "ALICE", DestinationStatus.Sent), Times.Once);
        environment.Verify(e => e.SetSentStatus("M1", "BOB", DestinationStatus.Sent), Times.Once);
    }

    /// <summary>A user in a context is the user the network configuration lists, or only a name for anyone else, such as an external system.</summary>
    [Fact]
    public void Users_AreTheListedUserInfo_OrJustAName()
    {
        Mock<IEngineContext> engine = new();
        engine.SetupGet(e => e.Users).Returns(new Dictionary<string, UserInfo> { ["ALICE"] = new UserInfo { Name = "ALICE", Role = UserRole.Server } });
        Mock<INetworkEnvironment> environment = new();
        environment.Setup(e => e.CreateEngineContext()).Returns(engine.Object);

        NetworkReceivedContext<TestFrame, TestMessagePriority, TestLevel, TestAspect> listed = new(environment.Object, new TestFrame(), FrameOrigin.Peer, "ALICE");
        NetworkReceivedContext<TestFrame, TestMessagePriority, TestLevel, TestAspect> external = new(environment.Object, new TestFrame(), FrameOrigin.ExternalSystem, "FEED");

        Assert.Equal(UserRole.Server, listed.SourceUser.Role);
        Assert.Equal("FEED", external.SourceUser.Name);
        Assert.Null(external.SourceUser.Role);
    }

    /// <summary>The auto forward targets never include the current user.</summary>
    [Fact]
    public async Task GetAutoForwardTargets_LeavesOutTheCurrentUser()
    {
        Mock<IEngineContext> engine = new();
        engine.SetupGet(e => e.Users).Returns(new Dictionary<string, UserInfo>());
        engine.SetupGet(e => e.CurrentUser).Returns(new UserInfo { Name = "ME" });
        Mock<INetworkEnvironment> environment = new();
        environment.Setup(e => e.CreateEngineContext()).Returns(engine.Object);
        environment.Setup(e => e.GetAutoForwardTargets("Alerts")).ReturnsAsync(["BOB", "ME", "CAROL"]);
        NetworkConnectedContext<TestFrame, TestMessagePriority, TestLevel, TestAspect> context = new(environment.Object, "BOB");

        Assert.Equal(["BOB", "CAROL"], await context.GetAutoForwardTargets("Alerts"));
    }

    /// <summary>Sending to the interfaces hands the priority and frame to the environment.</summary>
    [Fact]
    public async Task SendInterface_HandsTheFrameToTheEnvironment()
    {
        Mock<INetworkEnvironment> environment = new();
        NetworkConnectedContext<TestFrame, TestMessagePriority, TestLevel, TestAspect> context = Build(environment);
        TestFrame frame = new() { Body = "Hi" };

        await context.SendInterface(TestMessagePriority.High, frame);

        environment.Verify(e => e.SendInterface(TestMessagePriority.High, frame), Times.Once);
    }
}
