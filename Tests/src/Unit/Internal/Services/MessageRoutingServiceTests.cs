namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="MessageRoutingService"/> using mocked peer infrastructure.</summary>
public sealed class MessageRoutingServiceTests
{
    private readonly ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
    private readonly IEngineController format = new TestEngineController();

    private sealed class FakePeerService : IPeerService
    {
        public event Func<object, Task>? FrameDelivered;
        public event Func<string, string, Task>? ConfirmationReceived;
        public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;
#pragma warning disable CS0067
        public event Func<string, Task>? UserConnected;
        public event Func<string, Task>? UserDisconnected;
#pragma warning restore CS0067

        public IReadOnlyList<string> GetConnectedUsers() => [];
        public bool IsUserConnected(string userName) => false;
        public Task<bool> SendPacket(string userName, object packet, CancellationToken cancellation = default) => Task.FromResult(true);

        public List<(string User, TestFrame Message)> Sent { get; } = [];
        public List<TestFrame> DeliveredLocally { get; } = [];
        public bool ReturnSuccess { get; set; } = true;

        public Task Start(CancellationToken cancellation) => Task.CompletedTask;

        public Task<bool> Send(string userName, object message, CancellationToken cancellation = default)
        {
            Sent.Add((userName, (TestFrame)message));
            return Task.FromResult(ReturnSuccess);
        }

        public async Task DeliverLocal(object payload)
        {
            DeliveredLocally.Add((TestFrame)payload);
            if (FrameDelivered is not null) { await FrameDelivered(payload); }
        }

        public async Task FireDeliveryStatusChanged(string messageId, string user, DestinationStatus status)
        {
            if (DeliveryStatusChanged is not null) { await DeliveryStatusChanged(messageId, user, status); }
        }

        public async Task FireConfirmationReceived(string messageId, string confirmingUser)
        {
            if (ConfirmationReceived is not null) { await ConfirmationReceived(messageId, confirmingUser); }
        }
    }

    private sealed class FakeExternalSystem() : ExternalSystemBase<TestFrame>("Fake", TimeSpan.FromMilliseconds(20), TimeSpan.FromSeconds(30))
    {
        public List<TestFrame> SentMessages { get; } = [];
        public bool ReturnSuccess { get; set; } = true;

        protected override Task<bool> TryConnect(CancellationToken cancellation) => Task.FromResult(true);
        protected override Task Disconnect() => Task.CompletedTask;

        protected override Task<bool> Send(TestFrame message)
        {
            SentMessages.Add(message);
            return Task.FromResult(ReturnSuccess);
        }
    }

    private static async Task<(FakeExternalSystem System, CancellationTokenSource Cts, Task StartTask)> StartConnectedExternalServer()
    {
        FakeExternalSystem system = new();
        CancellationTokenSource cts = new();
        Task startTask = system.Start(cts.Token);

        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!system.IsConnected)
        {
            if (DateTime.UtcNow > deadline) { throw new TimeoutException("External server did not connect in time."); }
            await Task.Delay(10);
        }

        return (system, cts, startTask);
    }

    /// <summary>Verifies that Route returns a non-empty uppercase hex GUID as the message ID.</summary>
    [Fact]
    public async Task RouteAsync_ReturnsNonEmptyMessageId()
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, format, loggerFactory);
        SendMessagePayload payload = new()
        {
            Subject = "Hello",
            Body = "World",
            Addresses = [new AddressPayload { UserName = "TargetUser", Type = "To" }]
        };

        (string messageId, IReadOnlyList<UserDeliveryResult> _) = await service.Route("SourceUser", payload, default);

        Assert.False(string.IsNullOrEmpty(messageId));
        Assert.True(Guid.TryParseExact(messageId, "N", out _));
    }

    /// <summary>Verifies that Route sets the built message's priority from the payload's Priority.</summary>
    [Fact]
    public async Task RouteAsync_SetsMessagePriorityFromPayload()
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, format, loggerFactory);
        SendMessagePayload payload = new()
        {
            Subject = "Hello",
            Body = "World",
            Addresses = [new AddressPayload { UserName = "TargetUser", Type = "To" }],
            Priority = 2
        };

        await service.Route("SourceUser", payload, default);

        Assert.Equal(2, fake.Sent[0].Message.Priority);
    }

    /// <summary>Verifies that Route sends exactly once to each unique destination user, deduplicating addresses.</summary>
    [Fact]
    public async Task RouteAsync_SendsToEachUniqueTargetUser()
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, format, loggerFactory);
        SendMessagePayload payload = new()
        {
            Subject = "Multi",
            Body = "Body",
            Addresses =
            [
                new AddressPayload { UserName = "Alpha", Type = "To" },
                new AddressPayload { UserName = "Beta", Type = "Cc" },
                new AddressPayload { UserName = "Alpha", Type = "Cc" }
            ]
        };

        await service.Route("Source", payload, default);

        Assert.Equal(2, fake.Sent.Count);
        Assert.Contains(fake.Sent, s => s.User.Equals("Alpha", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fake.Sent, s => s.User.Equals("Beta", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>An external address is information for the user only: nothing is sent to it, it gets no delivery result, and a group of the same name is not expanded, but it stays on the message with its instructions.</summary>
    [Fact]
    public async Task RouteAsync_ExternalAddress_TakesNoActionButStaysOnTheMessage()
    {
        FakePeerService fake = new();
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.UserGroups).Returns(new Dictionary<string, IReadOnlyList<string>> { ["OMAHA"] = ["Gamma"] });
        MessageRoutingService service = new(fake, controller.Object, loggerFactory);
        SendMessagePayload payload = new()
        {
            Subject = "Hi",
            Body = "Body",
            Addresses =
            [
                new AddressPayload { UserName = "Alpha", Type = "To" },
                new AddressPayload { UserName = "OMAHA", Type = "External", Information = "Deliver to Eastside Office" },
                new AddressPayload { UserName = "Source", Type = "External" }
            ]
        };

        (string _, IReadOnlyList<UserDeliveryResult> results) = await service.Route("Source", payload, default);

        Assert.Equal(["Alpha"], fake.Sent.Select(s => s.User));
        Assert.Equal(["Alpha"], results.Select(r => r.UserName));
        Assert.Empty(fake.DeliveredLocally);
        TestFrame sent = fake.Sent[0].Message;
        TestAddressEntry external = Assert.Single(sent.Addresses, a => a.UserName == "OMAHA");
        Assert.Equal("External", external.Type);
        Assert.Equal("Deliver to Eastside Office", external.Information);
        Assert.Equal(3, sent.Addresses.Count);
    }

    /// <summary>A message addressed only to external addresses is routed to nobody and returns no results.</summary>
    [Fact]
    public async Task RouteAsync_OnlyExternalAddresses_SendsToNobody()
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, format, loggerFactory);
        SendMessagePayload payload = new() { Subject = "Hi", Body = "Body", Addresses = [new AddressPayload { UserName = "OMAHA", Type = "External" }] };

        (string messageId, IReadOnlyList<UserDeliveryResult> results) = await service.Route("Source", payload, default);

        Assert.NotEmpty(messageId);
        Assert.Empty(results);
        Assert.Empty(fake.Sent);
    }

    /// <summary>Verifies that Route still returns a message ID even when all peer sends fail.</summary>
    [Fact]
    public async Task RouteAsync_WhenPeerSendFails_StillReturnsMessageId()
    {
        FakePeerService fake = new() { ReturnSuccess = false };
        MessageRoutingService service = new(fake, format, loggerFactory);
        SendMessagePayload payload = new()
        {
            Subject = "Fail",
            Body = "Body",
            Addresses = [new AddressPayload { UserName = "Unreachable", Type = "To" }]
        };

        (string messageId, IReadOnlyList<UserDeliveryResult> results) = await service.Route("Source", payload, default);
        Assert.False(string.IsNullOrEmpty(messageId));
        Assert.Single(results);
        Assert.False(results[0].Success);
    }

    /// <summary>Addressing a group sends to all member users with AddressedVia populated.</summary>
    [Fact]
    public async Task RouteAsync_GroupAddress_ExpandsToMemberUsers()
    {
        NetworkConfig config = new()
        {
            UserGroups = new Dictionary<string, List<string>>
            {
                ["OPS"] = ["ALPHA", "BETA"]
            }
        };
        IEngineController groups = new TestEngineController(config);
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, groups, loggerFactory);

        SendMessagePayload payload = new()
        {
            Subject = "Broadcast",
            Body = "Body",
            Addresses = [new AddressPayload { UserName = "OPS", Type = "To" }]
        };

        (string _, IReadOnlyList<UserDeliveryResult> results) = await service.Route("SOURCE", payload, default);

        Assert.Equal(2, fake.Sent.Count);
        Assert.Contains(results, r => r.UserName.Equals("ALPHA", StringComparison.OrdinalIgnoreCase)
                                   && r.AddressedVia.Contains("OPS", StringComparer.OrdinalIgnoreCase));
        Assert.Contains(results, r => r.UserName.Equals("BETA", StringComparison.OrdinalIgnoreCase)
                                   && r.AddressedVia.Contains("OPS", StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Nested groups are fully expanded to leaf users.</summary>
    [Fact]
    public async Task RouteAsync_NestedGroupAddress_ExpandsToLeafUsers()
    {
        NetworkConfig config = new()
        {
            UserGroups = new Dictionary<string, List<string>>
            {
                ["INNER"] = ["ALPHA"],
                ["OUTER"] = ["INNER", "BETA"]
            }
        };
        IEngineController groups = new TestEngineController(config);
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, groups, loggerFactory);

        SendMessagePayload payload = new()
        {
            Subject = "Nested",
            Body = "Body",
            Addresses = [new AddressPayload { UserName = "OUTER", Type = "To" }]
        };

        await service.Route("SOURCE", payload, default);

        Assert.Equal(2, fake.Sent.Count);
        Assert.Contains(fake.Sent, s => s.User.Equals("ALPHA", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fake.Sent, s => s.User.Equals("BETA", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Sending to the current user skips the network entirely, delivers locally, and confirms immediately.</summary>
    [Fact]
    public async Task RouteAsync_ToOwnUser_DeliversLocallyAndConfirmsImmediately()
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, format, loggerFactory);

        List<(string MessageId, string User, DestinationStatus Status)> statusEvents = [];
        service.DeliveryStatusChanged += (msgId, user, status) =>
        {
            statusEvents.Add((msgId, user, status));
            return Task.CompletedTask;
        };

        SendMessagePayload payload = new()
        {
            Subject = "Self",
            Body = "Body",
            Addresses = [new AddressPayload { UserName = "SOURCE", Type = "To" }]
        };

        (string messageId, IReadOnlyList<UserDeliveryResult> results) = await service.Route("SOURCE", payload, default);

        Assert.Empty(fake.Sent);
        Assert.Single(fake.DeliveredLocally);
        Assert.Equal(messageId, fake.DeliveredLocally[0].MessageId);

        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.Equal("SOURCE", results[0].UserName, ignoreCase: true);

        Assert.Single(statusEvents);
        Assert.Equal(messageId, statusEvents[0].MessageId);
        Assert.Equal(DestinationStatus.Confirmed, statusEvents[0].Status);
    }

    /// <summary>Sending to a mix of self and a remote user delivers locally to self and over the network to the remote user.</summary>
    [Fact]
    public async Task RouteAsync_ToSelfAndRemoteUser_HandlesBothIndependently()
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, format, loggerFactory);

        SendMessagePayload payload = new()
        {
            Subject = "Mixed",
            Body = "Body",
            Addresses =
            [
                new AddressPayload { UserName = "SOURCE", Type = "To" },
                new AddressPayload { UserName = "REMOTE", Type = "To" }
            ]
        };

        (string _, IReadOnlyList<UserDeliveryResult> results) = await service.Route("SOURCE", payload, default);

        Assert.Single(fake.Sent);
        Assert.Equal("REMOTE", fake.Sent[0].User, ignoreCase: true);
        Assert.Single(fake.DeliveredLocally);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.UserName.Equals("SOURCE", StringComparison.OrdinalIgnoreCase) && r.Success);
        Assert.Contains(results, r => r.UserName.Equals("REMOTE", StringComparison.OrdinalIgnoreCase) && r.Success);
    }

    /// <summary>A delivery status from the peer service is forwarded to DeliveryStatusChanged unchanged, for every status value.</summary>
    [Theory]
    [InlineData(DestinationStatus.Sending)]
    [InlineData(DestinationStatus.Sent)]
    [InlineData(DestinationStatus.Failed)]
    [InlineData(DestinationStatus.Confirmed)]
    public async Task PeerDeliveryStatusChanged_ForwardsStatusUnchanged(DestinationStatus status)
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, format, loggerFactory);

        List<DestinationStatus> statuses = [];
        service.DeliveryStatusChanged += (_, _, s) =>
        {
            statuses.Add(s);
            return Task.CompletedTask;
        };

        await fake.FireDeliveryStatusChanged("MSG1", "ALPHA", status);

        Assert.Single(statuses);
        Assert.Equal(status, statuses[0]);
    }

    /// <summary>A confirmation received from the peer service is re-raised as a Read status change for the confirming user.</summary>
    [Fact]
    public async Task PeerConfirmationReceived_ReRaisesAsReadStatus()
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, format, loggerFactory);

        List<(string MessageId, string User, DestinationStatus Status)> changes = [];
        service.DeliveryStatusChanged += (messageId, user, status) =>
        {
            changes.Add((messageId, user, status));
            return Task.CompletedTask;
        };

        await fake.FireConfirmationReceived("MSG1", "ALPHA");

        (string messageId, string user, DestinationStatus status) = Assert.Single(changes);
        Assert.Equal("MSG1", messageId);
        Assert.Equal("ALPHA", user);
        Assert.Equal(DestinationStatus.Read, status);
    }

    /// <summary>With ExternalServer configured, remote sends go to it exactly once, regardless of recipient count, instead of dialing each peer individually.</summary>
    [Fact]
    public async Task RouteAsync_ExternalServerConfigured_SendsOnceToExternalServerNotPeer()
    {
        (FakeExternalSystem externalServer, CancellationTokenSource cts, Task startTask) = await StartConnectedExternalServer();
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.ExternalServer).Returns(externalServer);
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, controller.Object, loggerFactory);

        SendMessagePayload payload = new()
        {
            Subject = "ViaExternalServer",
            Body = "Body",
            Addresses =
            [
                new AddressPayload { UserName = "Alpha", Type = "To" },
                new AddressPayload { UserName = "Beta", Type = "To" }
            ]
        };

        (string _, IReadOnlyList<UserDeliveryResult> results) = await service.Route("Source", payload, default);

        Assert.Empty(fake.Sent);
        Assert.Single(externalServer.SentMessages);
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Success));

        cts.Cancel();
        await startTask;
    }

    /// <summary>With ExternalServer configured, a failed external-server send is reflected as a failure for every remote recipient.</summary>
    [Fact]
    public async Task RouteAsync_ExternalServerSendFails_AllRemoteResultsFail()
    {
        (FakeExternalSystem externalServer, CancellationTokenSource cts, Task startTask) = await StartConnectedExternalServer();
        externalServer.ReturnSuccess = false;
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.ExternalServer).Returns(externalServer);
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, controller.Object, loggerFactory);

        SendMessagePayload payload = new()
        {
            Subject = "Fail",
            Body = "Body",
            Addresses = [new AddressPayload { UserName = "Alpha", Type = "To" }]
        };

        (string _, IReadOnlyList<UserDeliveryResult> results) = await service.Route("Source", payload, default);

        Assert.Single(results);
        Assert.False(results[0].Success);

        cts.Cancel();
        await startTask;
    }

    /// <summary>With ExternalServer configured, sending to the current user still delivers locally instead of going through the external server.</summary>
    [Fact]
    public async Task RouteAsync_ExternalServerConfigured_SelfSendStillDeliversLocally()
    {
        (FakeExternalSystem externalServer, CancellationTokenSource cts, Task startTask) = await StartConnectedExternalServer();
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.ExternalServer).Returns(externalServer);
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, controller.Object, loggerFactory);

        SendMessagePayload payload = new()
        {
            Subject = "Self",
            Body = "Body",
            Addresses = [new AddressPayload { UserName = "SOURCE", Type = "To" }]
        };

        await service.Route("SOURCE", payload, default);

        Assert.Single(fake.DeliveredLocally);
        Assert.Empty(externalServer.SentMessages);

        cts.Cancel();
        await startTask;
    }

    /// <summary>RouteFrame reads its addresses and security level from the message object itself, via IEngineController, rather than from a SendMessagePayload.</summary>
    [Fact]
    public async Task RouteMessage_ReadsAddressesFromMessageItself()
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, format, loggerFactory);
        TestFrame message = new() { Subject = "Hi", Body = "Body" };
        format.SetAddresses(message, [new MessageAddress { UserName = "TargetUser", Type = AddressType.To }]);

        await service.RouteFrame("SourceUser", message, default);

        Assert.Single(fake.Sent);
        Assert.Equal("TargetUser", fake.Sent[0].User, ignoreCase: true);
        Assert.Same(message, fake.Sent[0].Message);
    }

    /// <summary>RouteFrame overwrites the message's own MessageId/FromUser/SentAt with a freshly generated ID, the given fromUser, and the current time, regardless of what the caller set them to.</summary>
    [Fact]
    public async Task RouteMessage_OverwritesMessageIdFromUserAndSentAt()
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, format, loggerFactory);
        TestFrame message = new() { MessageId = "STALE-ID", FromUser = "WRONG-USER", SentAt = new DateTime(2000, 1, 1) };
        format.SetAddresses(message, [new MessageAddress { UserName = "TargetUser", Type = AddressType.To }]);
        DateTime before = DateTime.UtcNow;

        (string messageId, _) = await service.RouteFrame("SourceUser", message, default);

        Assert.NotEqual("STALE-ID", messageId);
        Assert.True(Guid.TryParseExact(messageId, "N", out _));
        Assert.Equal(messageId, message.MessageId);
        Assert.Equal("SourceUser", message.FromUser, ignoreCase: true);
        Assert.InRange(message.SentAt, before, DateTime.UtcNow);
    }

    /// <summary>RouteFrame applies the same security-level filtering as Route, reading the blocking level from the message itself.</summary>
    [Fact]
    public async Task RouteMessage_BlocksDestinationsBelowTheMessagesSecurityLevel()
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.SecurityLevels).Returns((IReadOnlyList<SecurityLevel>)[new SecurityLevel { Name = "LOW", Color = "#000" }, new SecurityLevel { Name = "HIGH", Color = "#000" }]);
        controller.Setup(c => c.GetUserSecurityLevel("Cleared")).Returns("HIGH");
        controller.Setup(c => c.GetUserSecurityLevel("Blocked")).Returns("LOW");
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, controller.Object, loggerFactory);
        TestFrame message = new() { Subject = "Secret", Body = "Body", SecurityLevel = "HIGH" };
        controller.Object.SetAddresses(message, [new MessageAddress { UserName = "Cleared", Type = AddressType.To }, new MessageAddress { UserName = "Blocked", Type = AddressType.To }]);

        (_, IReadOnlyList<UserDeliveryResult> results) = await service.RouteFrame("SourceUser", message, default);

        Assert.Contains(fake.Sent, s => s.User.Equals("Cleared", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fake.Sent, s => s.User.Equals("Blocked", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, r => r.UserName.Equals("Blocked", StringComparison.OrdinalIgnoreCase) && !r.Success);
    }
}
