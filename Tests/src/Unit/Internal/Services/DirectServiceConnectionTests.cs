namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="DirectServiceConnection"/> event wiring and delegation.</summary>
public sealed class DirectServiceConnectionTests
{
    private static DirectServiceConnection Build(
        out FakePeerService fakePeer,
        out Mock<IUserService> userMock,
        out Mock<IEntryService> entryMock,
        out Mock<TestEngineController> engineMock,
        out MessageEvents events,
        out Mock<INetworkProcessing> processing)
    {
        fakePeer = new FakePeerService();
        userMock = new Mock<IUserService>();
        entryMock = new Mock<IEntryService>();
        engineMock = new Mock<TestEngineController> { CallBase = true };
        events = new MessageEvents();
        processing = new Mock<INetworkProcessing>();
        Mock<IIdGenerator> ids = new();
        ids.Setup(i => i.Next()).ReturnsAsync("ID-1");
        return new DirectServiceConnection(userMock.Object, engineMock.Object, fakePeer, entryMock.Object, events, processing.Object, ids.Object, LoggerFactory.Create(_ => { }));
    }

    private static DirectServiceConnection Build(out Mock<IUserService> user, out Mock<IEntryService> entry, out Mock<INetworkProcessing> processing, out MessageEvents events)
        => Build(out _, out user, out entry, out _, out events, out processing);

    /// <summary>GetUserInfo delegates to IUserService.GetCurrentUserInfo and returns the result.</summary>
    [Fact]
    public async Task GetUserInfo_WhenInstalled_ReturnsUserInfo()
    {
        DirectServiceConnection conn = Build(out _, out Mock<IUserService> user, out _, out _, out _, out _);
        UserInfo info = new() { Name = "ALPHA" };
        user.Setup(s => s.GetCurrentUserInfo()).Returns(info);

        Assert.Same(info, await conn.GetUserInfo());
    }

    /// <summary>GetUserInfo returns null when no user is installed.</summary>
    [Fact]
    public async Task GetUserInfo_WhenNotInstalled_ReturnsNull()
    {
        DirectServiceConnection conn = Build(out _, out Mock<IUserService> user, out _, out _, out _, out _);
        user.Setup(s => s.GetCurrentUserInfo()).Returns((UserInfo?)null);

        Assert.Null(await conn.GetUserInfo());
    }

    /// <summary>GetUserNames returns the names from the directory as a list.</summary>
    [Fact]
    public async Task GetUserNames_ReturnsUserNamesFromDirectory()
    {
        DirectServiceConnection conn = Build(out _, out _, out _, out Mock<TestEngineController> dir, out _, out _);
        dir.Setup(d => d.Users).Returns((IReadOnlyList<string>)["ALPHA", "BETA"]);

        Assert.Equal(["ALPHA", "BETA"], await conn.GetUserNames());
    }

    /// <summary>GetUserNames returns an empty list when the directory throws.</summary>
    [Fact]
    public async Task GetUserNames_WhenDirectoryThrows_ReturnsEmptyList()
    {
        DirectServiceConnection conn = Build(out _, out _, out _, out Mock<TestEngineController> dir, out _, out _);
        dir.Setup(d => d.Users).Throws(new IOException("network error"));

        Assert.Empty(await conn.GetUserNames());
    }

    /// <summary>GetConnectedUsers returns whatever IPeerService.GetConnectedUsers reports.</summary>
    [Fact]
    public async Task GetConnectedUsers_DelegatesToPeerService()
    {
        DirectServiceConnection conn = Build(out FakePeerService peer, out _, out _, out _, out _, out _);
        peer.Connected.UnionWith(["ALPHA", "BETA"]);

        Assert.Equal(["ALPHA", "BETA"], (await conn.GetConnectedUsers()).Order());
    }

    /// <summary>InstallUser delegates to IUserService.Install and returns its result.</summary>
    [Fact]
    public async Task InstallUser_DelegatesToUserService()
    {
        DirectServiceConnection conn = Build(out _, out Mock<IUserService> user, out _, out _, out _, out _);
        UserInfo info = new() { Name = "BRAVO" };
        user.Setup(s => s.Install("CODE1", It.IsAny<CancellationToken>())).ReturnsAsync(info);

        Assert.Same(info, await conn.InstallUser("CODE1"));
    }

    /// <summary>A message the host's processor records as received is raised to the connection's listeners.</summary>
    [Fact]
    public async Task Connect_ThenMessageReceived_IsRaised()
    {
        DirectServiceConnection conn = Build(out _, out _, out _, out MessageEvents events);
        await conn.Connect();
        List<Message> raised = [];
        conn.MessageReceived += message => { raised.Add(message); return Task.CompletedTask; };
        Message message = new() { Id = "M1", FromUser = "BOB", Body = "hi", Addresses = [], SentAt = DateTime.UtcNow, Priority = TestMessagePriority.Normal };

        await events.RaiseMessageReceived(message);

        Assert.Same(message, Assert.Single(raised));
    }

    /// <summary>A delivery status change is raised to the connection's listeners.</summary>
    [Fact]
    public async Task Connect_ThenDeliveryStatusChanged_IsRaised()
    {
        DirectServiceConnection conn = Build(out _, out _, out _, out MessageEvents events);
        await conn.Connect();
        List<DeliveryStatusChangedEvent> raised = [];
        conn.DeliveryStatusChanged += change => { raised.Add(change); return Task.CompletedTask; };

        await events.RaiseDeliveryStatusChanged(new DeliveryStatusChangedEvent { MessageId = "M1", UserName = "BOB", Status = DestinationStatus.Sent });

        Assert.Equal("BOB", Assert.Single(raised).UserName);
    }

    /// <summary>SendMessage builds the message, stores it in the Outbox and hands it to the network processor, which does the sending; the result carries its identifier and whether it is an alert.</summary>
    [Fact]
    public async Task SendMessage_StoresTheMessage_AndHandsItToTheProcessor()
    {
        DirectServiceConnection conn = Build(out Mock<IUserService> user, out Mock<IEntryService> entry, out Mock<INetworkProcessing> processing, out _);
        user.Setup(s => s.GetCurrentUserInfo()).Returns(new UserInfo { Name = "ME" });
        Message? stored = null;
        entry.Setup(e => e.StoreSentMessage(It.IsAny<Message>())).Callback<Message>(message => stored = message).ReturnsAsync(new MessageEntity());

        SendMessageResult? result = await conn.SendMessage("hello", [new AddressRequest { UserName = "BOB", Type = "Cc", Information = "note" }], TestMessagePriority.Level3, "TAG");

        Assert.NotNull(result);
        Assert.Equal("ID-1", result.MessageId);
        Assert.NotNull(stored);
        Assert.Equal(("ID-1", "ME", "hello", "TAG", TestMessagePriority.Level3), (stored.Id, stored.FromUser, stored.Body, stored.Tag, stored.Priority));
        MessageAddress address = Assert.Single(stored.Addresses);
        Assert.Equal(("BOB", AddressType.Cc, "note"), (address.UserName, address.Type, address.Information));
        processing.Verify(p => p.Sent(stored), Times.Once);
    }

    /// <summary>Without an installed user nothing is stored or sent and the result is null.</summary>
    [Fact]
    public async Task SendMessage_NoUser_ReturnsNull()
    {
        DirectServiceConnection conn = Build(out Mock<IUserService> user, out Mock<IEntryService> entry, out Mock<INetworkProcessing> processing, out _);
        user.Setup(s => s.GetCurrentUserInfo()).Returns((UserInfo?)null);

        Assert.Null(await conn.SendMessage("hello", []));
        entry.Verify(e => e.StoreSentMessage(It.IsAny<Message>()), Times.Never);
        processing.Verify(p => p.Sent(It.IsAny<Message>()), Times.Never);
    }

    /// <summary>Marking a message read tells the network processor, so it can tell the sender, and announces the change.</summary>
    [Fact]
    public async Task MarkMessageRead_TellsTheProcessor()
    {
        DirectServiceConnection conn = Build(out Mock<IUserService> _, out Mock<IEntryService> entry, out Mock<INetworkProcessing> processing, out _);
        MessageEntity entity = new() { MessageId = "M1", Message = new MessageData { Id = "M1", FromUser = "BOB", Body = "hi" } };
        entry.Setup(e => e.MarkMessageRead("M1")).ReturnsAsync(entity);
        List<DeliveryStatusChangedEvent> raised = [];
        await conn.Connect();
        conn.DeliveryStatusChanged += change => { raised.Add(change); return Task.CompletedTask; };

        Assert.True(await conn.MarkMessageRead("M1"));

        processing.Verify(p => p.Read(It.Is<Message>(message => message.Id == "M1" && message.FromUser == "BOB")), Times.Once);
        Assert.Equal(DestinationStatus.Read, Assert.Single(raised).Status);
    }

    /// <summary>A message that is not there, or already read, changes nothing.</summary>
    [Fact]
    public async Task MarkMessageRead_NothingChanged_ReturnsFalse()
    {
        DirectServiceConnection conn = Build(out Mock<IUserService> _, out Mock<IEntryService> entry, out Mock<INetworkProcessing> processing, out _);
        entry.Setup(e => e.MarkMessageRead("M1")).ReturnsAsync((MessageEntity?)null);

        Assert.False(await conn.MarkMessageRead("M1"));
        processing.Verify(p => p.Read(It.IsAny<Message>()), Times.Never);
    }
}
