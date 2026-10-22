namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="ServiceConnection{TPriority, TLevel, TAspect}"/>.</summary>
public sealed class ServiceConnectionTests
{
    private static Message MakeMessage() => new() { Id = "M1", FromUser = "ALICE", Body = "Hi", Addresses = [], SentAt = DateTime.UtcNow, Priority = TestMessagePriority.High, MessageLevel = TestLevel.Secret };

    /// <summary>A message the engine received reaches the host typed by its own enums.</summary>
    [Fact]
    public void MessageReceived_IsRaisedTypedByTheHostsEnums()
    {
        Mock<IEngineConnection> engine = new();
        ServiceConnection<TestMessagePriority, TestLevel, TestAspect, NoProfile> connection = new(engine.Object);
        Message<TestMessagePriority, TestLevel, TestAspect>? seen = null;
        connection.MessageReceived += message => { seen = message; return Task.CompletedTask; };

        engine.Raise(e => e.MessageReceived += null!, MakeMessage());

        Assert.NotNull(seen);
        Assert.Equal((TestMessagePriority.High, TestLevel.Secret, (TestAspect?)null), (seen.Priority, seen.MessageLevel, seen.MessageAspect));
    }

    /// <summary>The user's info reaches the host typed by its own profile type, and a node with no current user has none.</summary>
    [Fact]
    public async Task GetUserInfo_IsTypedByTheHostsProfile()
    {
        Mock<IEngineConnection> engine = new();
        engine.SetupSequence(e => e.GetUserInfo(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserInfo<object> { Name = "ALICE", Role = UserRole.Server, Profile = new TestProfile("4B", 2) })
            .ReturnsAsync((IUserInfo?)null);
        ServiceConnection<TestMessagePriority, TestLevel, TestAspect, TestProfile> connection = new(engine.Object);

        UserInfo<TestProfile>? info = await connection.GetUserInfo();

        Assert.Equal(("ALICE", UserRole.Server, new TestProfile("4B", 2)), (info!.Name, info.Role, info.Profile));
        Assert.Null(await connection.GetUserInfo());
    }

    /// <summary>A send hands the host's enum members to the engine as plain enums.</summary>
    [Fact]
    public async Task SendMessage_PassesTheEnumMembersOn()
    {
        Mock<IEngineConnection> engine = new();
        ServiceConnection<TestMessagePriority, TestLevel, TestAspect, NoProfile> connection = new(engine.Object);

        await connection.SendMessage("Hi", [], TestMessagePriority.Flash, "TAG", TestLevel.Internal);

        engine.Verify(e => e.SendMessage("Hi", It.IsAny<List<AddressRequest>>(), TestMessagePriority.Flash, "TAG", TestLevel.Internal, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
