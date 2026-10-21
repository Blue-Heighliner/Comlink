namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="Network{TFrame, TPriority, TLevel, TAspect}"/>.</summary>
public sealed class NetworkTests
{
    private delegate void DestinationsCallback(Enum? minimumLevel, IEnumerable<string> targets, out IReadOnlySet<string> excluded);

    private readonly Mock<INetworkEnvironment> environment = new();
    private readonly Mock<IUserCatalog> users = new();

    private Network<TestFrame, TestMessagePriority, TestLevel, TestAspect> Build() => new(environment.Object, users.Object);

    /// <summary>Setting the sent status for several users sets it for each of them.</summary>
    [Fact]
    public async Task SetSentStatus_ForSeveralUsers_SetsEachOne()
    {
        await Build().SetSentStatus("M1", ["ALICE", "BOB"], DestinationStatus.Sent);

        environment.Verify(e => e.SetSentStatus("M1", "ALICE", DestinationStatus.Sent), Times.Once);
        environment.Verify(e => e.SetSentStatus("M1", "BOB", DestinationStatus.Sent), Times.Once);
    }

    /// <summary>The auto forward targets never include the current user.</summary>
    [Fact]
    public async Task GetAutoForwardTargets_LeavesOutTheCurrentUser()
    {
        users.SetupGet(u => u.CurrentUser).Returns(new UserInfo { Name = "ME" });
        environment.Setup(e => e.GetAutoForwardTargets("Alerts")).ReturnsAsync(["BOB", "ME", "CAROL"]);

        Assert.Equal(["BOB", "CAROL"], await Build().GetAutoForwardTargets("Alerts"));
    }

    /// <summary>Sending to the interfaces, to a user and to the external systems hands the priority and frame to the environment.</summary>
    [Fact]
    public async Task Sends_HandTheFrameToTheEnvironment()
    {
        TestFrame frame = new() { Body = "Hi" };
        environment.Setup(e => e.Send("BOB", TestMessagePriority.Low, frame)).ReturnsAsync(true);
        Network<TestFrame, TestMessagePriority, TestLevel, TestAspect> service = Build();

        await service.SendInterface(TestMessagePriority.High, frame);
        await service.SendToExternalSystems(frame);
        bool sent = await service.Send("BOB", TestMessagePriority.Low, frame);

        Assert.True(sent);
        environment.Verify(e => e.SendInterface(TestMessagePriority.High, frame), Times.Once);
        environment.Verify(e => e.SendToExternalSystems(frame), Times.Once);
    }

    /// <summary>A message is for the users of all its addresses except the external ones, handed to the environment with the message's own level as the minimum.</summary>
    [Fact]
    public void GetDestinations_OfAMessage_LeavesOutExternalAddresses_AndUsesTheMessagesLevel()
    {
        IReadOnlySet<string> excluded = new HashSet<string>(["ERIN"]);
        IReadOnlySet<string> destinations = new HashSet<string>(["ALICE", "BOB"]);
        List<string> asked = [];
        environment.Setup(e => e.GetDestinations(TestLevel.Secret, It.IsAny<IEnumerable<string>>(), out excluded))
            .Callback(new DestinationsCallback((Enum? _, IEnumerable<string> targets, out IReadOnlySet<string> left) =>
            {
                asked.AddRange(targets);
                left = excluded;
            }))
            .Returns(destinations);
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
            Priority = TestMessagePriority.Normal,
            MessageLevel = TestLevel.Secret
        };

        IReadOnlySet<string> result = Build().GetDestinations(message, out IReadOnlySet<string> left);

        Assert.Same(destinations, result);
        Assert.Same(excluded, left);
        Assert.Equal(["OPS", "ERIN"], asked);
    }
}
