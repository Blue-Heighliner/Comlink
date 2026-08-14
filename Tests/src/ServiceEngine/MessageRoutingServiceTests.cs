namespace BlueHeighliner.Comlink.Tests.ServiceEngine;

/// <summary>Unit tests for <see cref="MessageRoutingService"/> using mocked peer infrastructure.</summary>
public sealed class MessageRoutingServiceTests
{
    private static readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });
    private static readonly IUserDirectory _noGroups = new DefaultUserDirectory();
    private static readonly IMessageFormat Format = new TestMessageFormat();

    private sealed class FakePeerService : IPeerService
    {
        public event Func<object, Task>? MessageDelivered;
        public event Func<string, string, Task>? ConfirmationReceived;
        public event Func<string, string, OftDeliveryStatus, Task>? DeliveryStatusChanged;

        public List<(string User, TestMessage Message)> Sent { get; } = [];
        public List<TestMessage> DeliveredLocally { get; } = [];
        public bool ReturnSuccess { get; set; } = true;

        public Task Start(CancellationToken cancellation) => Task.CompletedTask;

        public Task<bool> Send(string userName, object message, CancellationToken cancellation = default)
        {
            Sent.Add((userName, (TestMessage)message));
            return Task.FromResult(ReturnSuccess);
        }

        public async Task DeliverLocal(object payload)
        {
            DeliveredLocally.Add((TestMessage)payload);
            if (MessageDelivered is not null) await MessageDelivered(payload);
        }

        public async Task FireDeliveryStatusChanged(string messageId, string user, OftDeliveryStatus status)
        {
            if (DeliveryStatusChanged is not null) await DeliveryStatusChanged(messageId, user, status);
        }

        public async Task FireConfirmationReceived(string messageId, string confirmingUser)
        {
            if (ConfirmationReceived is not null) await ConfirmationReceived(messageId, confirmingUser);
        }
    }

    // ── Route basics ──────────────────────────────────────────────────────────

    /// <summary>Verifies that Route returns a non-empty uppercase hex GUID as the message ID.</summary>
    [Fact]
    public async Task RouteAsync_ReturnsNonEmptyMessageId()
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, _noGroups, Format, _loggerFactory);
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
        MessageRoutingService service = new(fake, _noGroups, Format, _loggerFactory);
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
        MessageRoutingService service = new(fake, _noGroups, Format, _loggerFactory);
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

    /// <summary>Verifies that Route still returns a message ID even when all peer sends fail.</summary>
    [Fact]
    public async Task RouteAsync_WhenPeerSendFails_StillReturnsMessageId()
    {
        FakePeerService fake = new() { ReturnSuccess = false };
        MessageRoutingService service = new(fake, _noGroups, Format, _loggerFactory);
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

    // ── Group expansion ───────────────────────────────────────────────────────

    /// <summary>Addressing a group sends to all member users with AddressedVia populated.</summary>
    [Fact]
    public async Task RouteAsync_GroupAddress_ExpandsToMemberUsers()
    {
        EngineConfig config = new()
        {
            UserGroups = new Dictionary<string, List<string>>
            {
                ["OPS"] = ["ALPHA", "BETA"]
            }
        };
        IUserDirectory groups = new ConfiguredUserDirectory(new DefaultUserDirectory(), config);
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, groups, Format, _loggerFactory);

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
        EngineConfig config = new()
        {
            UserGroups = new Dictionary<string, List<string>>
            {
                ["INNER"] = ["ALPHA"],
                ["OUTER"] = ["INNER", "BETA"]
            }
        };
        IUserDirectory groups = new ConfiguredUserDirectory(new DefaultUserDirectory(), config);
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, groups, Format, _loggerFactory);

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

    // ── Self-delivery ─────────────────────────────────────────────────────────

    /// <summary>Sending to the current user skips the network entirely, delivers locally, and confirms immediately.</summary>
    [Fact]
    public async Task RouteAsync_ToOwnUser_DeliversLocallyAndConfirmsImmediately()
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, _noGroups, Format, _loggerFactory);

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
        MessageRoutingService service = new(fake, _noGroups, Format, _loggerFactory);

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

    // ── OFT delivery status ──────────────────────────────────────────────────

    /// <summary>An Acknowledged OFT status from the peer service is mapped to Confirmed.</summary>
    [Fact]
    public async Task PeerDeliveryStatusChanged_Acknowledged_MapsToConfirmed()
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, _noGroups, Format, _loggerFactory);

        List<DestinationStatus> statuses = [];
        service.DeliveryStatusChanged += (_, _, status) =>
        {
            statuses.Add(status);
            return Task.CompletedTask;
        };

        await fake.FireDeliveryStatusChanged("MSG1", "ALPHA", OftDeliveryStatus.Acknowledged);

        Assert.Single(statuses);
        Assert.Equal(DestinationStatus.Confirmed, statuses[0]);
    }

    /// <summary>A Cancelled OFT status from the peer service is mapped to Failed.</summary>
    [Fact]
    public async Task PeerDeliveryStatusChanged_Cancelled_MapsToFailed()
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, _noGroups, Format, _loggerFactory);

        List<DestinationStatus> statuses = [];
        service.DeliveryStatusChanged += (_, _, status) =>
        {
            statuses.Add(status);
            return Task.CompletedTask;
        };

        await fake.FireDeliveryStatusChanged("MSG1", "ALPHA", OftDeliveryStatus.Cancelled);

        Assert.Single(statuses);
        Assert.Equal(DestinationStatus.Failed, statuses[0]);
    }

    /// <summary>A Sent OFT status from the peer service is mapped to Sent.</summary>
    [Fact]
    public async Task PeerDeliveryStatusChanged_Sent_MapsToSent()
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, _noGroups, Format, _loggerFactory);

        List<DestinationStatus> statuses = [];
        service.DeliveryStatusChanged += (_, _, status) =>
        {
            statuses.Add(status);
            return Task.CompletedTask;
        };

        await fake.FireDeliveryStatusChanged("MSG1", "ALPHA", OftDeliveryStatus.Sent);

        Assert.Single(statuses);
        Assert.Equal(DestinationStatus.Sent, statuses[0]);
    }

    /// <summary>Queued/Sending/Interrupted/Resumed OFT statuses are all mapped to Sending.</summary>
    [Theory]
    [InlineData(OftDeliveryStatus.Queued)]
    [InlineData(OftDeliveryStatus.Sending)]
    [InlineData(OftDeliveryStatus.Interrupted)]
    [InlineData(OftDeliveryStatus.Resumed)]
    public async Task PeerDeliveryStatusChanged_InFlightStatuses_MapToSending(OftDeliveryStatus oftStatus)
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, _noGroups, Format, _loggerFactory);

        List<DestinationStatus> statuses = [];
        service.DeliveryStatusChanged += (_, _, status) =>
        {
            statuses.Add(status);
            return Task.CompletedTask;
        };

        await fake.FireDeliveryStatusChanged("MSG1", "ALPHA", oftStatus);

        Assert.Single(statuses);
        Assert.Equal(DestinationStatus.Sending, statuses[0]);
    }

    // ── Read confirmation ─────────────────────────────────────────────────────

    /// <summary>A confirmation received from the peer service is re-raised as a Read status change for the confirming user.</summary>
    [Fact]
    public async Task PeerConfirmationReceived_ReRaisesAsReadStatus()
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, _noGroups, Format, _loggerFactory);

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
}
