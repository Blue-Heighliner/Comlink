namespace BlueHeighliner.Comlink.Tests.Unit.Public.Testing;

/// <summary>Unit tests for the service fakes of <c>BlueHeighliner.Comlink.Testing</c>: each answers what it was given, follows later changes, and records what a component does with the service.</summary>
public sealed class FakeServicesTests
{
    private static readonly UserInfo<NoProfile> alice = new() { Name = "ALICE" };
    private static readonly UserInfo<NoProfile> bob = new() { Name = "BOB" };

    private static Message<TestMessagePriority, TestLevel, TestAspect> Message(params MessageAddress[] addresses)
        => new() { Id = "M1", FromUser = "ALICE", Body = "Hi", Addresses = addresses, SentAt = DateTime.UtcNow, Priority = TestMessagePriority.Normal };

    /// <summary>The user catalog answers the users, groups and connections it is given, finds users whatever their case, and follows later changes.</summary>
    [Fact]
    public void FakeUserCatalog_AnswersWhatIsGiven_AndFollowsChanges()
    {
        FakeUserCatalog<NoProfile> catalog = new(alice, [alice, bob], [new GroupInfo { Id = "OPS", Members = ["ALICE", "BOB"] }]);

        Assert.Same(alice, catalog.CurrentUser);
        Assert.Equal(["ALICE", "BOB"], catalog.Users.Keys.Order());
        Assert.Same(bob, catalog.Users["bob"]);
        Assert.Empty(catalog.ConnectedUsers);
        Assert.Equal(["ALICE", "BOB"], catalog.Groups["ops"].Members);

        catalog.ConnectedUsers = new Dictionary<string, UserInfo<NoProfile>> { ["BOB"] = bob };
        catalog.CurrentUser = bob;

        Assert.Equal("BOB", Assert.Single(catalog.ConnectedUsers).Key);
        Assert.Same(bob, catalog.CurrentUser);
        Assert.Equal(["ALICE"], new FakeUserCatalog<NoProfile>(alice).Users.Keys);
    }

    /// <summary>The network fake accepts every send and finishes every other operation, records each in a list of its own, and answers what it is set to.</summary>
    [Fact]
    public async Task FakeNetwork_AcceptsAndRecordsEverything_AndAnswersWhatItIsSetTo()
    {
        FakeNetwork<TestFrame, TestMessagePriority, TestLevel, TestAspect> fake = new();
        INetwork<TestFrame, TestMessagePriority, TestLevel, TestAspect> network = fake;
        TestFrame frame = new();
        Message<TestMessagePriority, TestLevel, TestAspect> message = Message();

        Assert.True(await network.Send("BOB", TestMessagePriority.Low, frame));
        await network.SetSentStatus("M1", ["BOB", "CAROL"], DestinationStatus.Sent);
        await network.SetReceivedStatus("M2", DestinationStatus.Read);
        await network.SendInterface(TestMessagePriority.High, frame);
        await network.SendToExternalSystems(frame);
        await network.StoreMessage(message);
        await network.ReceiveMessage(message);
        network.SetNetworkIndicator(true);
        Assert.Empty(await network.FindStoredMessages(new RetrievalCriteria()));
        Assert.Empty(await network.GetAutoForwardTargets("Alerts"));

        Assert.Equal(("BOB", TestMessagePriority.Low, frame), Assert.Single(fake.Sent));
        Assert.Equal([("M1", "BOB", DestinationStatus.Sent), ("M1", "CAROL", DestinationStatus.Sent)], fake.SentStatuses);
        Assert.Equal(("M2", DestinationStatus.Read), Assert.Single(fake.ReceivedStatuses));
        Assert.Equal((TestMessagePriority.High, frame), Assert.Single(fake.SentToInterfaces));
        Assert.Same(frame, Assert.Single(fake.SentToExternalSystems));
        Assert.Same(message, Assert.Single(fake.Stored));
        Assert.Same(message, Assert.Single(fake.ReceivedMessages));
        Assert.Equal([true], fake.NetworkIndicators);

        fake.SendResult = false;
        fake.AutoForwardTargets = ["BOB"];
        fake.FoundMessages = [message];

        Assert.False(await network.Send("BOB", TestMessagePriority.Low, frame));
        Assert.Equal(["BOB"], await network.GetAutoForwardTargets("Alerts"));
        Assert.Single(await network.FindStoredMessages(new RetrievalCriteria()));
    }

    /// <summary>The network fake expands destinations to the distinct targets, or the addresses of a message other than external ones, and leaves out no one.</summary>
    [Fact]
    public void FakeNetwork_GetDestinations_AnswersTheDistinctTargets()
    {
        INetwork<TestFrame, TestMessagePriority, TestLevel, TestAspect> network = new FakeNetwork<TestFrame, TestMessagePriority, TestLevel, TestAspect>();

        IReadOnlySet<string> ofTargets = network.GetDestinations(null, out IReadOnlySet<string> excluded, "BOB", "CAROL", "BOB");
        IReadOnlySet<string> ofMessage = network.GetDestinations(Message(new MessageAddress { UserName = "ERIN", Type = AddressType.To }, new MessageAddress { UserName = "OMAHA", Type = AddressType.External }), out IReadOnlySet<string> excludedOfMessage);

        Assert.True(ofTargets.SetEquals(["BOB", "CAROL"]));
        Assert.True(ofMessage.SetEquals(["ERIN"]));
        Assert.Empty(excluded);
        Assert.Empty(excludedOfMessage);
    }

    /// <summary>The service connection fake connects, answers what it is set to, accepts and records a send and a mark read, and raises the engine's events to whoever subscribed.</summary>
    [Fact]
    public async Task FakeServiceConnection_AnswersRecordsAndRaisesEvents()
    {
        FakeServiceConnection<TestMessagePriority, TestLevel, TestAspect, NoProfile> fake = new(alice) { UserNames = ["ALICE", "BOB"] };
        IServiceConnection<TestMessagePriority, TestLevel, TestAspect, NoProfile> connection = fake;
        List<string> seen = [];
        connection.MessageReceived += message => { seen.Add(message.Id); return Task.CompletedTask; };
        connection.DeliveryStatusChanged += change => { seen.Add(change.MessageId); return Task.CompletedTask; };

        await connection.Connect();
        Assert.Same(alice, await connection.GetUserInfo());
        Assert.Equal(["ALICE", "BOB"], await connection.GetUserNames());
        Assert.Empty(await connection.GetConnectedUsers());
        Assert.Equal("MESSAGE", (await connection.SendMessage("Hi", [new AddressRequest { UserName = "BOB" }], TestMessagePriority.High, "URGENT"))?.MessageId);
        Assert.True(await connection.MarkMessageRead("M1"));

        await fake.RaiseMessageReceived(Message());
        await fake.RaiseDeliveryStatusChanged(new DeliveryStatusChangedEvent { MessageId = "M2" });

        Assert.Equal(["M1", "M2"], seen);
        Assert.Equal(1, fake.ConnectCount);
        Assert.Equal(["M1"], fake.MarkedRead);
        FakeServiceConnection<TestMessagePriority, TestLevel, TestAspect, NoProfile>.SentMessage sent = Assert.Single(fake.Sent);
        Assert.Equal(("Hi", TestMessagePriority.High, "URGENT"), (sent.Body, sent.Priority, sent.Tag));
        Assert.Equal("BOB", Assert.Single(sent.Addresses).UserName);
    }

    /// <summary>The logger fake records every entry in order, with or without an exception, and refuses an entry with no identifier as the engine's does.</summary>
    [Fact]
    public void FakeLogger_RecordsEveryEntry_AndRefusesNoId()
    {
        FakeLogger logger = new();
        InvalidOperationException exception = new("bad");

        logger.Log("APP", "T1", "first");
        logger.Log("ERROR", "T2", exception, "second");

        Assert.Equal([new FakeLogEntry("APP", "T1", "first", null), new FakeLogEntry("ERROR", "T2", "second", exception)], logger.Entries);
        Assert.Throws<ArgumentException>(() => logger.Log("APP", " ", "no id"));
        Assert.Equal(2, logger.Entries.Count);
    }

    /// <summary>The debug mode fake says what it was told, and follows a change.</summary>
    [Fact]
    public void FakeDebugMode_SaysWhatItWasTold()
    {
        FakeDebugMode mode = new();
        Assert.False(mode.IsDebug);

        mode.IsDebug = true;

        Assert.True(mode.IsDebug);
        Assert.True(new FakeDebugMode(true).IsDebug);
    }

    /// <summary>The data pool fake rents a separate buffer of exactly the size asked for each time.</summary>
    [Fact]
    public void FakeDataPool_RentsSeparateBuffersOfTheSizeAsked()
    {
        IDataPool pool = new FakeDataPool();

        using IMemoryOwner<byte> first = pool.Rent(8);
        using IMemoryOwner<byte> second = pool.Rent(8);
        first.Memory.Span[0] = 1;

        Assert.Equal(8, first.Memory.Length);
        Assert.Equal(0, second.Memory.Span[0]);
    }
}
