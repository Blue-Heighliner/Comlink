namespace BlueHeighliner.Comlink.Tests.Services;

/// <summary>Unit tests for <see cref="DirectServiceConnection"/> event wiring and delegation.</summary>
public sealed class DirectServiceConnectionTests
{
    // ── Test fakes for Func<T, Task> events ───────────────────────────────────

    private sealed class FakePeerService : IPeerService
    {
        public event Func<object, Task>? MessageDelivered;
#pragma warning disable CS0067
        public event Func<string, string, OftDeliveryStatus, Task>? DeliveryStatusChanged;
#pragma warning restore CS0067
        public Task Start(CancellationToken cancellation) => Task.CompletedTask;
        public Task<bool> Send(string siteName, object message, CancellationToken cancellation = default) => Task.FromResult(true);
        public Task DeliverLocal(object payload) => MessageDelivered is null ? Task.CompletedTask : MessageDelivered(payload);

        public async Task FireMessageDelivered(object payload)
        {
            if (MessageDelivered is not null) await MessageDelivered(payload);
        }
    }

    private sealed class FakeMessageRoutingService : IMessageRoutingService
    {
        public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;

        public (string MessageId, IReadOnlyList<SiteDeliveryResult> SiteResults)? RouteResult;

        public Task<(string MessageId, IReadOnlyList<SiteDeliveryResult> SiteResults)> Route(
            string fromSite, SendMessagePayload payload, CancellationToken cancellation)
        {
            if (RouteResult is null) throw new InvalidOperationException("RouteResult not configured");
            return Task.FromResult(RouteResult.Value);
        }

        public async Task FireDeliveryStatusChanged(string messageId, string site, DestinationStatus status)
        {
            if (DeliveryStatusChanged is not null) await DeliveryStatusChanged(messageId, site, status);
        }
    }

    private static DirectServiceConnection Build(
        out FakePeerService fakePeer,
        out FakeMessageRoutingService fakeRouting,
        out Mock<ISiteService> siteMock,
        out Mock<IEntryService> entryMock,
        out Mock<ISiteNameDirectory> dirMock)
    {
        fakePeer = new FakePeerService();
        fakeRouting = new FakeMessageRoutingService();
        siteMock = new Mock<ISiteService>();
        entryMock = new Mock<IEntryService>();
        dirMock = new Mock<ISiteNameDirectory>();
        Mock<ISiteCodeResolver> resolverMock = new();
        return new DirectServiceConnection(siteMock.Object, resolverMock.Object, dirMock.Object,
            fakeRouting, fakePeer, entryMock.Object, new TestMessageFormat());
    }

    // ── GetSiteInfo ───────────────────────────────────────────────────────────

    /// <summary>GetSiteInfo delegates to ISiteService.GetCurrentSiteInfo and returns the result.</summary>
    [Fact]
    public async Task GetSiteInfo_WhenInstalled_ReturnsSiteInfo()
    {
        DirectServiceConnection conn = Build(out _, out _, out Mock<ISiteService> site, out _, out _);
        SiteInfo info = new() { Name = "ALPHA", Code = "A1", EnvironmentTitle = "PROD", EnvironmentColor = "#FF0000" };
        site.Setup(s => s.GetCurrentSiteInfo()).Returns(info);

        SiteInfo? result = await conn.GetSiteInfo();

        Assert.Same(info, result);
    }

    /// <summary>GetSiteInfo returns null when no site is installed.</summary>
    [Fact]
    public async Task GetSiteInfo_WhenNotInstalled_ReturnsNull()
    {
        DirectServiceConnection conn = Build(out _, out _, out Mock<ISiteService> site, out _, out _);
        site.Setup(s => s.GetCurrentSiteInfo()).Returns((SiteInfo?)null);

        SiteInfo? result = await conn.GetSiteInfo();

        Assert.Null(result);
    }

    // ── GetSiteNames ──────────────────────────────────────────────────────────

    /// <summary>GetSiteNames returns the names from the directory as a list.</summary>
    [Fact]
    public async Task GetSiteNames_ReturnsSiteNamesFromDirectory()
    {
        DirectServiceConnection conn = Build(out _, out _, out _, out _, out Mock<ISiteNameDirectory> dir);
        dir.Setup(d => d.GetAllSiteNames(It.IsAny<CancellationToken>()))
           .ReturnsAsync((IReadOnlyList<string>)["ALPHA", "BETA"]);

        List<string> names = await conn.GetSiteNames();

        Assert.Equal(["ALPHA", "BETA"], names);
    }

    /// <summary>GetSiteNames returns an empty list when the directory throws.</summary>
    [Fact]
    public async Task GetSiteNames_WhenDirectoryThrows_ReturnsEmptyList()
    {
        DirectServiceConnection conn = Build(out _, out _, out _, out _, out Mock<ISiteNameDirectory> dir);
        dir.Setup(d => d.GetAllSiteNames(It.IsAny<CancellationToken>()))
           .ThrowsAsync(new IOException("network error"));

        List<string> names = await conn.GetSiteNames();

        Assert.Empty(names);
    }

    // ── InstallSite ───────────────────────────────────────────────────────────

    /// <summary>InstallSite delegates to ISiteService.Install and returns its result.</summary>
    [Fact]
    public async Task InstallSite_DelegatesToSiteService()
    {
        DirectServiceConnection conn = Build(out _, out _, out Mock<ISiteService> site, out _, out _);
        SiteInfo info = new() { Name = "BRAVO", Code = "B2", EnvironmentTitle = "TEST", EnvironmentColor = "#0000FF" };
        site.Setup(s => s.Install("CODE1", It.IsAny<CancellationToken>())).ReturnsAsync(info);

        SiteInfo? result = await conn.InstallSite("CODE1");

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
            FromSite = "REMOTE",
            Subject = "Hi",
            Body = "Body text",
            Addresses = [new TestAddressEntry { SiteName = "LOCAL", Type = "To" }],
            SentAt = new DateTime(2025, 7, 4, 12, 0, 0, DateTimeKind.Utc)
        };
        await peer.FireMessageDelivered(payload);

        Assert.NotNull(received);
        Assert.Equal("MSG1", received.MessageId);
        Assert.Equal("REMOTE", received.FromSite);
        Assert.Equal("Hi", received.Subject);
        Assert.Equal("Body text", received.Body);
        Assert.Single(received.Addresses);
        Assert.Equal("LOCAL", received.Addresses[0].SiteName);
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
            DeliveryStatuses = [new DeliveryStatus { SiteName = "DEST", Status = DestinationStatus.Confirmed, AddressedVia = [] }]
        };
        entry.Setup(e => e.UpdateDeliveryStatus("MSG2", "DEST", DestinationStatus.Confirmed))
             .ReturnsAsync(fakeEntity);
        await conn.Connect();

        DeliveryStatusChangedEvent? evt = null;
        conn.DeliveryStatusChanged += e => { evt = e; return Task.CompletedTask; };

        await routing.FireDeliveryStatusChanged("MSG2", "DEST", DestinationStatus.Confirmed);

        Assert.NotNull(evt);
        Assert.Equal("MSG2", evt.MessageId);
        Assert.Equal("DEST", evt.SiteName);
        Assert.Equal(DestinationStatus.Confirmed, evt.Status);
        entry.Verify(e => e.UpdateDeliveryStatus("MSG2", "DEST", DestinationStatus.Confirmed), Times.Once);
    }

    // ── SendMessage ───────────────────────────────────────────────────────────

    /// <summary>SendMessage returns null when no site is installed.</summary>
    [Fact]
    public async Task SendMessage_WhenNotInstalled_ReturnsNull()
    {
        DirectServiceConnection conn = Build(out _, out _, out Mock<ISiteService> site, out _, out _);
        site.Setup(s => s.GetCurrentSiteInfo()).Returns((SiteInfo?)null);

        SendMessageResult? result = await conn.SendMessage("Subject", "Body", []);

        Assert.Null(result);
    }

    /// <summary>SendMessage delegates to the routing service and maps the result to SendMessageResult.</summary>
    [Fact]
    public async Task SendMessage_DelegatesToRoutingServiceAndMapsResult()
    {
        DirectServiceConnection conn = Build(out _, out FakeMessageRoutingService routing,
            out Mock<ISiteService> site, out _, out _);
        site.Setup(s => s.GetCurrentSiteInfo()).Returns(new SiteInfo
        {
            Name = "ALPHA", Code = "A", EnvironmentTitle = "T", EnvironmentColor = "#000"
        });
        IReadOnlyList<SiteDeliveryResult> siteResults =
            [new SiteDeliveryResult { SiteName = "DEST", Success = true, AddressedVia = [] }];
        routing.RouteResult = ("MSGID1", siteResults);

        SendMessageResult? result = await conn.SendMessage("Hi", "Body", [new AddressRequest { SiteName = "DEST" }]);

        Assert.NotNull(result);
        Assert.Equal("MSGID1", result.MessageId);
        Assert.Single(result.SiteResults);
        Assert.Equal("DEST", result.SiteResults[0].SiteName);
        Assert.True(result.SiteResults[0].Success);
    }
}
