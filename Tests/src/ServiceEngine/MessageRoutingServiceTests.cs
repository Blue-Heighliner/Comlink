namespace BlueHeighliner.Comlink.Tests.ServiceEngine;

/// <summary>Unit tests for <see cref="MessageRoutingService"/> using mocked peer infrastructure.</summary>
public sealed class MessageRoutingServiceTests
{
    private static readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });
    private static readonly ISiteGroupProvider _noGroups = new SiteGroupProvider(new EngineConfig());
    private static readonly IMessageFormat Format = new TestMessageFormat();

    private sealed class FakePeerService : IPeerService
    {
        public event Func<object, Task>? MessageDelivered;
        public event Func<string, string, OftDeliveryStatus, Task>? DeliveryStatusChanged;

        public List<(string Site, TestMessage Message)> Sent { get; } = [];
        public List<TestMessage> DeliveredLocally { get; } = [];
        public bool ReturnSuccess { get; set; } = true;

        public Task Start(CancellationToken cancellation) => Task.CompletedTask;

        public Task<bool> Send(string siteName, object message, CancellationToken cancellation = default)
        {
            Sent.Add((siteName, (TestMessage)message));
            return Task.FromResult(ReturnSuccess);
        }

        public async Task DeliverLocal(object payload)
        {
            DeliveredLocally.Add((TestMessage)payload);
            if (MessageDelivered is not null) await MessageDelivered(payload);
        }

        public async Task FireDeliveryStatusChanged(string messageId, string site, OftDeliveryStatus status)
        {
            if (DeliveryStatusChanged is not null) await DeliveryStatusChanged(messageId, site, status);
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
            Addresses = [new AddressPayload { SiteName = "TargetSite", Type = "To" }]
        };

        (string messageId, IReadOnlyList<SiteDeliveryResult> _) = await service.Route("SourceSite", payload, default);

        Assert.False(string.IsNullOrEmpty(messageId));
        Assert.True(Guid.TryParseExact(messageId, "N", out _));
    }

    /// <summary>Verifies that Route sends exactly once to each unique destination site, deduplicating addresses.</summary>
    [Fact]
    public async Task RouteAsync_SendsToEachUniqueTargetSite()
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, _noGroups, Format, _loggerFactory);
        SendMessagePayload payload = new()
        {
            Subject = "Multi",
            Body = "Body",
            Addresses =
            [
                new AddressPayload { SiteName = "Alpha", Type = "To" },
                new AddressPayload { SiteName = "Beta", Type = "Cc" },
                new AddressPayload { SiteName = "Alpha", Type = "Cc" }
            ]
        };

        await service.Route("Source", payload, default);

        Assert.Equal(2, fake.Sent.Count);
        Assert.Contains(fake.Sent, s => s.Site.Equals("Alpha", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fake.Sent, s => s.Site.Equals("Beta", StringComparison.OrdinalIgnoreCase));
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
            Addresses = [new AddressPayload { SiteName = "Unreachable", Type = "To" }]
        };

        (string messageId, IReadOnlyList<SiteDeliveryResult> results) = await service.Route("Source", payload, default);
        Assert.False(string.IsNullOrEmpty(messageId));
        Assert.Single(results);
        Assert.False(results[0].Success);
    }

    // ── Group expansion ───────────────────────────────────────────────────────

    /// <summary>Addressing a group sends to all member sites with AddressedVia populated.</summary>
    [Fact]
    public async Task RouteAsync_GroupAddress_ExpandsToMemberSites()
    {
        EngineConfig config = new()
        {
            SiteGroups = new Dictionary<string, List<string>>
            {
                ["OPS"] = ["ALPHA", "BETA"]
            }
        };
        ISiteGroupProvider groups = new SiteGroupProvider(config);
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, groups, Format, _loggerFactory);

        SendMessagePayload payload = new()
        {
            Subject = "Broadcast",
            Body = "Body",
            Addresses = [new AddressPayload { SiteName = "OPS", Type = "To" }]
        };

        (string _, IReadOnlyList<SiteDeliveryResult> results) = await service.Route("SOURCE", payload, default);

        Assert.Equal(2, fake.Sent.Count);
        Assert.Contains(results, r => r.SiteName.Equals("ALPHA", StringComparison.OrdinalIgnoreCase)
                                   && r.AddressedVia.Contains("OPS", StringComparer.OrdinalIgnoreCase));
        Assert.Contains(results, r => r.SiteName.Equals("BETA", StringComparison.OrdinalIgnoreCase)
                                   && r.AddressedVia.Contains("OPS", StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Nested groups are fully expanded to leaf sites.</summary>
    [Fact]
    public async Task RouteAsync_NestedGroupAddress_ExpandsToLeafSites()
    {
        EngineConfig config = new()
        {
            SiteGroups = new Dictionary<string, List<string>>
            {
                ["INNER"] = ["ALPHA"],
                ["OUTER"] = ["INNER", "BETA"]
            }
        };
        ISiteGroupProvider groups = new SiteGroupProvider(config);
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, groups, Format, _loggerFactory);

        SendMessagePayload payload = new()
        {
            Subject = "Nested",
            Body = "Body",
            Addresses = [new AddressPayload { SiteName = "OUTER", Type = "To" }]
        };

        await service.Route("SOURCE", payload, default);

        Assert.Equal(2, fake.Sent.Count);
        Assert.Contains(fake.Sent, s => s.Site.Equals("ALPHA", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fake.Sent, s => s.Site.Equals("BETA", StringComparison.OrdinalIgnoreCase));
    }

    // ── Self-delivery ─────────────────────────────────────────────────────────

    /// <summary>Sending to the current site skips the network entirely, delivers locally, and confirms immediately.</summary>
    [Fact]
    public async Task RouteAsync_ToOwnSite_DeliversLocallyAndConfirmsImmediately()
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, _noGroups, Format, _loggerFactory);

        List<(string MessageId, string Site, DestinationStatus Status)> statusEvents = [];
        service.DeliveryStatusChanged += (msgId, site, status) =>
        {
            statusEvents.Add((msgId, site, status));
            return Task.CompletedTask;
        };

        SendMessagePayload payload = new()
        {
            Subject = "Self",
            Body = "Body",
            Addresses = [new AddressPayload { SiteName = "SOURCE", Type = "To" }]
        };

        (string messageId, IReadOnlyList<SiteDeliveryResult> results) = await service.Route("SOURCE", payload, default);

        Assert.Empty(fake.Sent);
        Assert.Single(fake.DeliveredLocally);
        Assert.Equal(messageId, fake.DeliveredLocally[0].MessageId);

        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.Equal("SOURCE", results[0].SiteName, ignoreCase: true);

        Assert.Single(statusEvents);
        Assert.Equal(messageId, statusEvents[0].MessageId);
        Assert.Equal(DestinationStatus.Confirmed, statusEvents[0].Status);
    }

    /// <summary>Sending to a mix of self and a remote site delivers locally to self and over the network to the remote site.</summary>
    [Fact]
    public async Task RouteAsync_ToSelfAndRemoteSite_HandlesBothIndependently()
    {
        FakePeerService fake = new();
        MessageRoutingService service = new(fake, _noGroups, Format, _loggerFactory);

        SendMessagePayload payload = new()
        {
            Subject = "Mixed",
            Body = "Body",
            Addresses =
            [
                new AddressPayload { SiteName = "SOURCE", Type = "To" },
                new AddressPayload { SiteName = "REMOTE", Type = "To" }
            ]
        };

        (string _, IReadOnlyList<SiteDeliveryResult> results) = await service.Route("SOURCE", payload, default);

        Assert.Single(fake.Sent);
        Assert.Equal("REMOTE", fake.Sent[0].Site, ignoreCase: true);
        Assert.Single(fake.DeliveredLocally);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.SiteName.Equals("SOURCE", StringComparison.OrdinalIgnoreCase) && r.Success);
        Assert.Contains(results, r => r.SiteName.Equals("REMOTE", StringComparison.OrdinalIgnoreCase) && r.Success);
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
}
