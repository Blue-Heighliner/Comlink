namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="EngineFrameHandler{TFrame, TPriority, TLevel, TAspect}"/>.</summary>
public sealed class EngineFrameHandlerTests
{
    /// <summary>A message's destinations are handed to the handler without the users it cannot be sent to, who are marked failed first.</summary>
    [Fact]
    public async Task OnSent_MarksUsersBelowTheMessageLevelFailed_AndLeavesThemOutOfTheDestinations()
    {
        Mock<IEngineContext> engine = new();
        engine.SetupGet(e => e.Users).Returns(new Dictionary<string, UserInfo>());
        engine.Setup(e => e.GetGroupMembers(It.IsAny<string>())).Returns([]);
        Mock<INetworkEnvironment> environment = new();
        environment.Setup(e => e.CreateEngineContext()).Returns(engine.Object);
        environment.Setup(e => e.IsAtLeast("BOB", TestLevel.Secret)).Returns(true);
        environment.Setup(e => e.IsAtLeast("CAROL", TestLevel.Secret)).Returns(false);
        IReadOnlySet<string>? seen = null;
        Mock<IFrameHandler<TestFrame, TestMessagePriority, TestLevel, TestAspect>> handler = new();
        handler.Setup(p => p.OnSent(It.IsAny<INetworkSentContext<TestFrame, TestMessagePriority, TestLevel, TestAspect>>()))
            .Callback((INetworkSentContext<TestFrame, TestMessagePriority, TestLevel, TestAspect> context) => seen = context.Destinations)
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
            MessageLevel = TestLevel.Secret
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
        Mock<IEngineContext> engine = new();
        engine.SetupGet(e => e.Users).Returns(new Dictionary<string, UserInfo>());
        engine.Setup(e => e.GetGroupMembers(It.IsAny<string>())).Returns([]);
        Mock<INetworkEnvironment> environment = new();
        environment.Setup(e => e.CreateEngineContext()).Returns(engine.Object);
        environment.Setup(e => e.IsAtLeast(It.IsAny<string>(), It.IsAny<Enum>())).Returns(false);
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
            MessageLevel = TestLevel.Secret
        };

        await adapter.OnSent(environment.Object, message);

        handler.Verify(p => p.OnSent(It.IsAny<INetworkSentContext<TestFrame, TestMessagePriority, TestLevel, TestAspect>>()), Times.Never);
        environment.Verify(e => e.SetSentStatus("M1", "BOB", DestinationStatus.Failed), Times.Once);
    }
}
