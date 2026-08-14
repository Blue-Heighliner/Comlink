namespace BlueHeighliner.Comlink.Tests.Services;

/// <summary>Unit tests for <see cref="DirectServiceConnection"/> event wiring and delegation.</summary>
public sealed class DirectServiceConnectionTests
{
    // ── Test fakes for Func<T, Task> events ───────────────────────────────────

    private sealed class FakePeerService : IPeerService
    {
        public event Func<object, Task>? MessageDelivered;
#pragma warning disable CS0067
        public event Func<string, string, Task>? ConfirmationReceived;
        public event Func<string, string, OftDeliveryStatus, Task>? DeliveryStatusChanged;
#pragma warning restore CS0067
        public List<(string UserName, TestMessage Message)> Sent { get; } = [];
        public bool ReturnSuccess { get; set; } = true;

        public Task Start(CancellationToken cancellation) => Task.CompletedTask;
        public Task<bool> Send(string userName, object message, CancellationToken cancellation = default)
        {
            Sent.Add((userName, (TestMessage)message));
            return Task.FromResult(ReturnSuccess);
        }
        public Task DeliverLocal(object payload) => MessageDelivered is null ? Task.CompletedTask : MessageDelivered(payload);

        public async Task FireMessageDelivered(object payload)
        {
            if (MessageDelivered is not null) await MessageDelivered(payload);
        }
    }

    private sealed class FakeMessageRoutingService : IMessageRoutingService
    {
        public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;

        public (string MessageId, IReadOnlyList<UserDeliveryResult> UserResults)? RouteResult;
        public SendMessagePayload? LastPayload;

        public Task<(string MessageId, IReadOnlyList<UserDeliveryResult> UserResults)> Route(
            string fromUser, SendMessagePayload payload, CancellationToken cancellation)
        {
            LastPayload = payload;
            if (RouteResult is null) throw new InvalidOperationException("RouteResult not configured");
            return Task.FromResult(RouteResult.Value);
        }

        public async Task FireDeliveryStatusChanged(string messageId, string user, DestinationStatus status)
        {
            if (DeliveryStatusChanged is not null) await DeliveryStatusChanged(messageId, user, status);
        }
    }

    private static DirectServiceConnection Build(
        out FakePeerService fakePeer,
        out FakeMessageRoutingService fakeRouting,
        out Mock<IUserService> userMock,
        out Mock<IEntryService> entryMock,
        out Mock<IUserDirectory> dirMock)
    {
        fakePeer = new FakePeerService();
        fakeRouting = new FakeMessageRoutingService();
        userMock = new Mock<IUserService>();
        entryMock = new Mock<IEntryService>();
        dirMock = new Mock<IUserDirectory>();
        return new DirectServiceConnection(userMock.Object, dirMock.Object,
            fakeRouting, fakePeer, entryMock.Object, new TestMessageFormat());
    }

    // ── GetUserInfo ───────────────────────────────────────────────────────────

    /// <summary>GetUserInfo delegates to IUserService.GetCurrentUserInfo and returns the result.</summary>
    [Fact]
    public async Task GetUserInfo_WhenInstalled_ReturnsUserInfo()
    {
        DirectServiceConnection conn = Build(out _, out _, out Mock<IUserService> user, out _, out _);
        UserInfo info = new() { Name = "ALPHA", Code = "A1", EnvironmentTitle = "PROD", EnvironmentColor = "#FF0000" };
        user.Setup(s => s.GetCurrentUserInfo()).Returns(info);

        UserInfo? result = await conn.GetUserInfo();

        Assert.Same(info, result);
    }

    /// <summary>GetUserInfo returns null when no user is installed.</summary>
    [Fact]
    public async Task GetUserInfo_WhenNotInstalled_ReturnsNull()
    {
        DirectServiceConnection conn = Build(out _, out _, out Mock<IUserService> user, out _, out _);
        user.Setup(s => s.GetCurrentUserInfo()).Returns((UserInfo?)null);

        UserInfo? result = await conn.GetUserInfo();

        Assert.Null(result);
    }

    // ── GetUserNames ──────────────────────────────────────────────────────────

    /// <summary>GetUserNames returns the names from the directory as a list.</summary>
    [Fact]
    public async Task GetUserNames_ReturnsUserNamesFromDirectory()
    {
        DirectServiceConnection conn = Build(out _, out _, out _, out _, out Mock<IUserDirectory> dir);
        dir.Setup(d => d.GetAllUserNames(It.IsAny<CancellationToken>()))
           .ReturnsAsync((IReadOnlyList<string>)["ALPHA", "BETA"]);

        List<string> names = await conn.GetUserNames();

        Assert.Equal(["ALPHA", "BETA"], names);
    }

    /// <summary>GetUserNames returns an empty list when the directory throws.</summary>
    [Fact]
    public async Task GetUserNames_WhenDirectoryThrows_ReturnsEmptyList()
    {
        DirectServiceConnection conn = Build(out _, out _, out _, out _, out Mock<IUserDirectory> dir);
        dir.Setup(d => d.GetAllUserNames(It.IsAny<CancellationToken>()))
           .ThrowsAsync(new IOException("network error"));

        List<string> names = await conn.GetUserNames();

        Assert.Empty(names);
    }

    // ── InstallUser ───────────────────────────────────────────────────────────

    /// <summary>InstallUser delegates to IUserService.Install and returns its result.</summary>
    [Fact]
    public async Task InstallUser_DelegatesToUserService()
    {
        DirectServiceConnection conn = Build(out _, out _, out Mock<IUserService> user, out _, out _);
        UserInfo info = new() { Name = "BRAVO", Code = "B2", EnvironmentTitle = "TEST", EnvironmentColor = "#0000FF" };
        user.Setup(s => s.Install("CODE1", It.IsAny<CancellationToken>())).ReturnsAsync(info);

        UserInfo? result = await conn.InstallUser("CODE1");

        Assert.Same(info, result);
    }

    // ── MessageReceived event wiring ──────────────────────────────────────────

    /// <summary>After Connect, a MessageDelivered peer event is converted and re-raised as MessageReceived.</summary>
    [Fact]
    public async Task Connect_ThenMessageDelivered_RaisesMessageReceivedEvent()
    {
        DirectServiceConnection conn = Build(out FakePeerService peer, out _, out _, out _, out _);
        await conn.Connect();

        MessageReceivedEvent? received = null;
        conn.MessageReceived += evt => { received = evt; return Task.CompletedTask; };

        TestMessage payload = new()
        {
            MessageId = "MSG1",
            FromUser = "REMOTE",
            Subject = "Hi",
            Body = "Body text",
            Addresses = [new TestAddressEntry { UserName = "LOCAL", Type = "To" }],
            SentAt = new DateTime(2025, 7, 4, 12, 0, 0, DateTimeKind.Utc),
            Priority = 2
        };
        await peer.FireMessageDelivered(payload);

        Assert.NotNull(received);
        Assert.Equal("MSG1", received.MessageId);
        Assert.Equal("REMOTE", received.FromUser);
        Assert.Equal("Hi", received.Subject);
        Assert.Equal("Body text", received.Body);
        Assert.Single(received.Addresses);
        Assert.Equal("LOCAL", received.Addresses[0].UserName);
        Assert.Equal(2, received.Priority);
    }

    // ── DeliveryStatusChanged event wiring ────────────────────────────────────

    /// <summary>After Connect, a DeliveryStatusChanged routing event updates the entry service and fires the connection event.</summary>
    [Fact]
    public async Task Connect_ThenDeliveryStatusChanged_UpdatesEntryAndRaisesEvent()
    {
        DirectServiceConnection conn = Build(out _, out FakeMessageRoutingService routing,
            out _, out Mock<IEntryService> entry, out _);

        MessageEntity fakeEntity = new()
        {
            MessageId = "MSG2",
            DeliveryStatuses = [new DeliveryStatus { UserName = "DEST", Status = DestinationStatus.Confirmed, AddressedVia = [] }]
        };
        entry.Setup(e => e.UpdateDeliveryStatus("MSG2", "DEST", DestinationStatus.Confirmed))
             .ReturnsAsync(fakeEntity);
        await conn.Connect();

        DeliveryStatusChangedEvent? evt = null;
        conn.DeliveryStatusChanged += e => { evt = e; return Task.CompletedTask; };

        await routing.FireDeliveryStatusChanged("MSG2", "DEST", DestinationStatus.Confirmed);

        Assert.NotNull(evt);
        Assert.Equal("MSG2", evt.MessageId);
        Assert.Equal("DEST", evt.UserName);
        Assert.Equal(DestinationStatus.Confirmed, evt.Status);
        entry.Verify(e => e.UpdateDeliveryStatus("MSG2", "DEST", DestinationStatus.Confirmed), Times.Once);
    }

    // ── SendMessage ───────────────────────────────────────────────────────────

    /// <summary>SendMessage returns null when no user is installed.</summary>
    [Fact]
    public async Task SendMessage_WhenNotInstalled_ReturnsNull()
    {
        DirectServiceConnection conn = Build(out _, out _, out Mock<IUserService> user, out _, out _);
        user.Setup(s => s.GetCurrentUserInfo()).Returns((UserInfo?)null);

        SendMessageResult? result = await conn.SendMessage("Subject", "Body", []);

        Assert.Null(result);
    }

    /// <summary>SendMessage delegates to the routing service and maps the result to SendMessageResult.</summary>
    [Fact]
    public async Task SendMessage_DelegatesToRoutingServiceAndMapsResult()
    {
        DirectServiceConnection conn = Build(out _, out FakeMessageRoutingService routing,
            out Mock<IUserService> user, out _, out _);
        user.Setup(s => s.GetCurrentUserInfo()).Returns(new UserInfo
        {
            Name = "ALPHA", Code = "A", EnvironmentTitle = "T", EnvironmentColor = "#000"
        });
        IReadOnlyList<UserDeliveryResult> userResults =
            [new UserDeliveryResult { UserName = "DEST", Success = true, AddressedVia = [] }];
        routing.RouteResult = ("MSGID1", userResults);

        SendMessageResult? result = await conn.SendMessage("Hi", "Body", [new AddressRequest { UserName = "DEST" }]);

        Assert.NotNull(result);
        Assert.Equal("MSGID1", result.MessageId);
        Assert.Single(result.UserResults);
        Assert.Equal("DEST", result.UserResults[0].UserName);
        Assert.True(result.UserResults[0].Success);
    }

    /// <summary>SendMessage passes the priority argument through to the routing payload.</summary>
    [Fact]
    public async Task SendMessage_PassesPriorityThroughToPayload()
    {
        DirectServiceConnection conn = Build(out _, out FakeMessageRoutingService routing,
            out Mock<IUserService> user, out _, out _);
        user.Setup(s => s.GetCurrentUserInfo()).Returns(new UserInfo
        {
            Name = "ALPHA", Code = "A", EnvironmentTitle = "T", EnvironmentColor = "#000"
        });
        routing.RouteResult = ("MSGID1", []);

        await conn.SendMessage("Hi", "Body", [new AddressRequest { UserName = "DEST" }], priority: 3);

        Assert.NotNull(routing.LastPayload);
        Assert.Equal(3, routing.LastPayload.Priority);
    }

    // ── MarkMessageRead ───────────────────────────────────────────────────────

    /// <summary>MarkMessageRead returns false and sends nothing when EntryService reports no change (already read or not found).</summary>
    [Fact]
    public async Task MarkMessageRead_WhenEntryServiceReturnsNull_ReturnsFalse()
    {
        DirectServiceConnection conn = Build(out FakePeerService peer, out _, out _, out Mock<IEntryService> entry, out _);
        entry.Setup(e => e.MarkMessageRead("MSG1")).ReturnsAsync((MessageEntity?)null);

        bool result = await conn.MarkMessageRead("MSG1");

        Assert.False(result);
        Assert.Empty(peer.Sent);
    }

    /// <summary>MarkMessageRead sends a confirmation message back to the sender and raises a local Read status event.</summary>
    [Fact]
    public async Task MarkMessageRead_ForRemoteSender_SendsConfirmationAndRaisesReadEvent()
    {
        DirectServiceConnection conn = Build(out FakePeerService peer, out _, out Mock<IUserService> user, out Mock<IEntryService> entry, out _);
        user.Setup(s => s.GetCurrentUserInfo()).Returns(new UserInfo { Name = "LOCAL", Code = "A", EnvironmentTitle = "T", EnvironmentColor = "#000" });

        TestMessage stored = new() { MessageId = "MSG1", FromUser = "REMOTE" };
        MessageEntity entity = new() { MessageId = "MSG1", Message = stored };
        entry.Setup(e => e.MarkMessageRead("MSG1")).ReturnsAsync(entity);

        DeliveryStatusChangedEvent? evt = null;
        conn.DeliveryStatusChanged += e => { evt = e; return Task.CompletedTask; };

        bool result = await conn.MarkMessageRead("MSG1");

        Assert.True(result);
        Assert.NotNull(evt);
        Assert.Equal("MSG1", evt.MessageId);
        Assert.Equal(DestinationStatus.Read, evt.Status);

        (string userName, TestMessage confirmation) = Assert.Single(peer.Sent);
        Assert.Equal("REMOTE", userName);
        Assert.Equal("MSG1", confirmation.ConfirmationMessageId);
        Assert.Equal("LOCAL", confirmation.FromUser);
        Assert.NotEqual("MSG1", confirmation.MessageId);
    }

    /// <summary>MarkMessageRead for a self-addressed message updates the Outbox status directly instead of sending over the wire.</summary>
    [Fact]
    public async Task MarkMessageRead_ForSelfAddressedMessage_UpdatesStatusWithoutSending()
    {
        DirectServiceConnection conn = Build(out FakePeerService peer, out _, out Mock<IUserService> user, out Mock<IEntryService> entry, out _);
        user.Setup(s => s.GetCurrentUserInfo()).Returns(new UserInfo { Name = "LOCAL", Code = "A", EnvironmentTitle = "T", EnvironmentColor = "#000" });

        TestMessage stored = new() { MessageId = "MSG1", FromUser = "LOCAL" };
        MessageEntity entity = new() { MessageId = "MSG1", Message = stored };
        entry.Setup(e => e.MarkMessageRead("MSG1")).ReturnsAsync(entity);

        bool result = await conn.MarkMessageRead("MSG1");

        Assert.True(result);
        Assert.Empty(peer.Sent);
        entry.Verify(e => e.UpdateDeliveryStatus("MSG1", "LOCAL", DestinationStatus.Read), Times.Once);
    }
}
