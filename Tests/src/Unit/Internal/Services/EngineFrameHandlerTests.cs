namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="EngineFrameHandler{TFrame, TPriority, TLevel, TAspect}"/>.</summary>
public sealed class EngineFrameHandlerTests
{
    /// <summary>A connection, a disconnection, a received frame and a retrieval reach the handler carrying the names of the users, which the handler looks up in the user catalog if it needs more.</summary>
    [Fact]
    public async Task UserEvents_CarryTheNamesOfTheUsers()
    {
        Mock<INetworkEnvironment> environment = new();
        TestFrame frame = new();
        RetrievalCriteria criteria = new();
        List<string> seen = [];
        Mock<IFrameHandler<TestFrame, TestMessagePriority, TestLevel, TestAspect>> handler = new();
        handler.Setup(p => p.OnConnected(It.IsAny<INetworkConnectedContext>())).Callback((INetworkConnectedContext context) => seen.Add($"connected {context.TargetUser}")).Returns(Task.CompletedTask);
        handler.Setup(p => p.OnDisconnected(It.IsAny<INetworkDisconnectedContext>())).Callback((INetworkDisconnectedContext context) => seen.Add($"disconnected {context.TargetUser}")).Returns(Task.CompletedTask);
        handler.Setup(p => p.OnReceived(It.IsAny<INetworkReceivedContext<TestFrame>>())).Callback((INetworkReceivedContext<TestFrame> context) => seen.Add($"received {context.SourceUser} {context.Origin} {ReferenceEquals(context.Frame, frame)}")).Returns(Task.CompletedTask);
        handler.Setup(p => p.OnRetrieval(It.IsAny<INetworkRetrievalContext>())).Callback((INetworkRetrievalContext context) => seen.Add($"retrieval {context.Server} {ReferenceEquals(context.Criteria, criteria)}")).Returns(Task.CompletedTask);
        EngineFrameHandler<TestFrame, TestMessagePriority, TestLevel, TestAspect> adapter = new(handler.Object);

        await adapter.OnConnected(environment.Object, "ALICE");
        await adapter.OnDisconnected(environment.Object, "BOB");
        await adapter.OnReceived(environment.Object, frame, FrameOrigin.Interface, "CAROL");
        await adapter.OnRetrieval(environment.Object, "SERVER", criteria);

        Assert.Equal(["connected ALICE", "disconnected BOB", "received CAROL Interface True", "retrieval SERVER True"], seen);
    }

    /// <summary>A message's destinations are handed to the handler without the users it cannot be sent to, who are marked failed first.</summary>
    [Fact]
    public async Task OnSent_MarksUsersBelowTheLevelFailed_AndLeavesThemOutOfTheDestinations()
    {
        IReadOnlySet<string> excluded = new HashSet<string>(["CAROL"]);
        Mock<INetworkEnvironment> environment = new();
        environment.Setup(e => e.GetDestinations(TestLevel.Secret, It.IsAny<IEnumerable<string>>(), out excluded)).Returns(new HashSet<string>(["BOB"]));
        IReadOnlySet<string>? seen = null;
        Mock<IFrameHandler<TestFrame, TestMessagePriority, TestLevel, TestAspect>> handler = new();
        handler.Setup(p => p.OnSent(It.IsAny<INetworkSentContext<TestMessagePriority, TestLevel, TestAspect>>()))
            .Callback((INetworkSentContext<TestMessagePriority, TestLevel, TestAspect> context) => seen = context.Destinations)
            .Returns(Task.CompletedTask);
        EngineFrameHandler<TestFrame, TestMessagePriority, TestLevel, TestAspect> adapter = new(handler.Object);
        Message message = new()
        {
            Id = "M1",
            FromUser = "ALICE",
            Body = "Hi",
            Addresses = [new MessageAddress { UserName = "BOB", Type = AddressType.To }, new MessageAddress { UserName = "CAROL", Type = AddressType.Cc }],
            SentAt = DateTime.UtcNow,
            Priority = TestMessagePriority.Normal,
            Level = TestLevel.Secret
        };

        await adapter.OnSent(environment.Object, message);

        Assert.NotNull(seen);
        Assert.True(seen.SetEquals(["BOB"]));
        environment.Verify(e => e.SetSentStatus("M1", "CAROL", DestinationStatus.Failed), Times.Once);
        environment.Verify(e => e.SetSentStatus("M1", "BOB", It.IsAny<DestinationStatus>()), Times.Never);
    }

    /// <summary>A message with no destination left is not handed to the handler at all.</summary>
    [Fact]
    public async Task OnSent_WithNoDestinations_DoesNotCallTheHandler()
    {
        IReadOnlySet<string> excluded = new HashSet<string>(["BOB"]);
        Mock<INetworkEnvironment> environment = new();
        environment.Setup(e => e.GetDestinations(TestLevel.Secret, It.IsAny<IEnumerable<string>>(), out excluded)).Returns(new HashSet<string>());
        Mock<IFrameHandler<TestFrame, TestMessagePriority, TestLevel, TestAspect>> handler = new();
        EngineFrameHandler<TestFrame, TestMessagePriority, TestLevel, TestAspect> adapter = new(handler.Object);
        Message message = new()
        {
            Id = "M1",
            FromUser = "ALICE",
            Body = "Hi",
            Addresses = [new MessageAddress { UserName = "BOB", Type = AddressType.To }],
            SentAt = DateTime.UtcNow,
            Priority = TestMessagePriority.Normal,
            Level = TestLevel.Secret
        };

        await adapter.OnSent(environment.Object, message);

        handler.Verify(p => p.OnSent(It.IsAny<INetworkSentContext<TestMessagePriority, TestLevel, TestAspect>>()), Times.Never);
        environment.Verify(e => e.SetSentStatus("M1", "BOB", DestinationStatus.Failed), Times.Once);
    }
}
