namespace BlueHeighliner.Comlink.Tests.Unit.Testing;

/// <summary>Unit tests for the service mocks of <c>BlueHeighliner.Comlink.Testing</c>: each answers what it was given, follows later changes, and accepts what a component does with the service.</summary>
public sealed class MockServicesTests
{
    private static readonly UserInfo alice = new() { Name = "ALICE" };
    private static readonly UserInfo bob = new() { Name = "BOB" };

    private static Message<TestMessagePriority, TestLevel, TestAspect> Message(params MessageAddress[] addresses)
        => new() { Id = "M1", FromUser = "ALICE", Body = "Hi", Addresses = addresses, SentAt = DateTime.UtcNow, Priority = TestMessagePriority.Normal };

    /// <summary>The user catalog answers the users, groups and connections it is given, finds users whatever their case, and follows later changes.</summary>
    [Fact]
    public void MockUserCatalog_AnswersWhatIsGiven_AndFollowsChanges()
    {
        MockUserCatalog mock = new(alice, [alice, bob], [new GroupInfo { Id = "OPS", Members = ["ALICE", "BOB"] }]);
        IUserCatalog catalog = mock.Object;

        Assert.Same(alice, catalog.CurrentUser);
        Assert.Equal(["ALICE", "BOB"], catalog.Users.Keys.Order());
        Assert.Same(bob, catalog.Users["bob"]);
        Assert.Empty(catalog.ConnectedUsers);
        Assert.Equal(["ALICE", "BOB"], catalog.Groups["ops"].Members);

        mock.ConnectedUsers = new Dictionary<string, UserInfo> { ["BOB"] = bob };
        mock.CurrentUser = bob;

        Assert.Equal("BOB", Assert.Single(catalog.ConnectedUsers).Key);
        Assert.Same(bob, catalog.CurrentUser);
        Assert.Equal(["ALICE"], new MockUserCatalog(alice).Object.Users.Keys);
    }

    /// <summary>The network mock accepts every send and finishes every other operation, records them for verifying, and answers what it is set to.</summary>
    [Fact]
    public async Task MockNetwork_AcceptsEverything_AndAnswersWhatItIsSetTo()
    {
        MockNetwork<TestFrame, TestMessagePriority, TestLevel, TestAspect> mock = new();
        INetwork<TestFrame, TestMessagePriority, TestLevel, TestAspect> network = mock.Object;
        TestFrame frame = new();

        Assert.True(await network.Send("BOB", TestMessagePriority.Low, frame));
        await network.SetSentStatus("M1", ["BOB", "CAROL"], DestinationStatus.Sent);
        await network.SendInterface(TestMessagePriority.High, frame);
        await network.StoreMessage(Message());
        Assert.Empty(await network.FindStoredMessages(new RetrievalCriteria()));
        Assert.Empty(await network.GetAutoForwardTargets("Alerts"));

        mock.SendResult = false;
        mock.AutoForwardTargets = ["BOB"];
        mock.StoredMessages = [Message()];

        Assert.False(await network.Send("BOB", TestMessagePriority.Low, frame));
        Assert.Equal(["BOB"], await network.GetAutoForwardTargets("Alerts"));
        Assert.Single(await network.FindStoredMessages(new RetrievalCriteria()));
        mock.Verify(service => service.SendInterface(TestMessagePriority.High, frame), Times.Once);
        mock.Verify(service => service.SetSentStatus("M1", It.IsAny<IEnumerable<string>>(), DestinationStatus.Sent), Times.Once);
    }

    /// <summary>The network mock expands destinations to the distinct targets, or the addresses of a message other than external ones, and leaves out no one.</summary>
    [Fact]
    public void MockNetwork_GetDestinations_AnswersTheDistinctTargets()
    {
        INetwork<TestFrame, TestMessagePriority, TestLevel, TestAspect> network = new MockNetwork<TestFrame, TestMessagePriority, TestLevel, TestAspect>().Object;

        IReadOnlySet<string> ofTargets = network.GetDestinations(null, out IReadOnlySet<string> excluded, "BOB", "CAROL", "BOB");
        IReadOnlySet<string> ofMessage = network.GetDestinations(Message(new MessageAddress { UserName = "ERIN", Type = AddressType.To }, new MessageAddress { UserName = "OMAHA", Type = AddressType.External }), out IReadOnlySet<string> excludedOfMessage);

        Assert.True(ofTargets.SetEquals(["BOB", "CAROL"]));
        Assert.True(ofMessage.SetEquals(["ERIN"]));
        Assert.Empty(excluded);
        Assert.Empty(excludedOfMessage);
    }

    /// <summary>The service connection mock connects, answers what it is set to, accepts a send and a mark read, and raises the engine's events to whoever subscribed.</summary>
    [Fact]
    public async Task MockServiceConnection_AnswersAndRaisesEvents()
    {
        MockServiceConnection<TestMessagePriority, TestLevel, TestAspect> mock = new(alice) { UserNames = ["ALICE", "BOB"] };
        IServiceConnection<TestMessagePriority, TestLevel, TestAspect> connection = mock.Object;
        List<string> seen = [];
        connection.MessageReceived += message => { seen.Add(message.Id); return Task.CompletedTask; };
        connection.DeliveryStatusChanged += change => { seen.Add(change.MessageId); return Task.CompletedTask; };

        await connection.Connect();
        Assert.Same(alice, await connection.GetUserInfo());
        Assert.Equal(["ALICE", "BOB"], await connection.GetUserNames());
        Assert.Empty(await connection.GetConnectedUsers());
        Assert.Equal("MESSAGE", (await connection.SendMessage("Hi", [new AddressRequest { UserName = "BOB" }]))?.MessageId);
        Assert.True(await connection.MarkMessageRead("M1"));

        mock.RaiseMessageReceived(Message());
        mock.RaiseDeliveryStatusChanged(new DeliveryStatusChangedEvent { MessageId = "M2" });

        Assert.Equal(["M1", "M2"], seen);
        mock.Verify(c => c.Connect(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>The logger mock records every entry in order, with or without an exception.</summary>
    [Fact]
    public void MockLogger_RecordsEveryEntry()
    {
        MockLogger mock = new();
        InvalidOperationException exception = new("bad");

        mock.Object.Log("APP", "T1", "first");
        mock.Object.Log("ERROR", "T2", exception, "second");

        Assert.Equal([new MockLogEntry("APP", "T1", "first", null), new MockLogEntry("ERROR", "T2", "second", exception)], mock.Entries);
    }

    /// <summary>The debug mode mock says what it was told, and follows a change.</summary>
    [Fact]
    public void MockDebugMode_SaysWhatItWasTold()
    {
        MockDebugMode mock = new();
        Assert.False(mock.Object.IsDebug);

        mock.IsDebug = true;

        Assert.True(mock.Object.IsDebug);
        Assert.True(new MockDebugMode(true).Object.IsDebug);
    }

    /// <summary>The data pool mock rents a separate buffer of exactly the size asked for each time.</summary>
    [Fact]
    public void MockDataPool_RentsSeparateBuffersOfTheSizeAsked()
    {
        IDataPool pool = new MockDataPool().Object;

        using IMemoryOwner<byte> first = pool.Rent(8);
        using IMemoryOwner<byte> second = pool.Rent(8);
        first.Memory.Span[0] = 1;

        Assert.Equal(8, first.Memory.Length);
        Assert.Equal(0, second.Memory.Span[0]);
    }
}
