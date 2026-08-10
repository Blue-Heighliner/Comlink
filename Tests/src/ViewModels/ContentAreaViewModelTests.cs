namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="ContentAreaViewModel"/>.</summary>
public sealed class ContentAreaViewModelTests
{
    private sealed class FakeServiceConnection : IServiceConnection
    {
#pragma warning disable CS0067
        public event Func<MessageReceivedEvent, Task>? MessageReceived;
#pragma warning restore CS0067
        public event Func<DeliveryStatusChangedEvent, Task>? DeliveryStatusChanged;

        public Task Connect(CancellationToken cancellation = default) => Task.CompletedTask;
        public Task<SiteInfo?> GetSiteInfo(CancellationToken cancellation = default) => Task.FromResult<SiteInfo?>(null);
        public Task<List<string>> GetSiteNames(CancellationToken cancellation = default) => Task.FromResult(new List<string>());
        public Task<SiteInfo?> InstallSite(string siteCode, CancellationToken cancellation = default) => Task.FromResult<SiteInfo?>(null);
        public Task<SendMessageResult?> SendMessage(string subject, string body, List<AddressRequest> addresses, CancellationToken cancellation = default) => Task.FromResult<SendMessageResult?>(null);

        public async Task RaiseDeliveryStatusChanged(DeliveryStatusChangedEvent evt)
        {
            if (DeliveryStatusChanged is not null) await DeliveryStatusChanged(evt);
        }
    }

    private static readonly IMessageFormat Format = new TestMessageFormat();

    private static ContentAreaViewModel Build(out FakeServiceConnection connection, string homeText = "HOME")
    {
        connection = new FakeServiceConnection();
        Mock<IHomeContentProvider> home = new();
        home.Setup(h => h.GetHomeText()).Returns(homeText);
        Mock<IEntryService> entry = new();
        Mock<IMessageRepository> messages = new();
        Mock<IDraftRepository> drafts = new();
        Mock<INoteRepository> notes = new();
        Mock<IActivityLogRepository> activityLogs = new();
        ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
        return new ContentAreaViewModel(home.Object, entry.Object, connection, messages.Object,
            drafts.Object, notes.Object, activityLogs.Object, Format, loggerFactory);
    }

    // ── HomeText ──────────────────────────────────────────────────────────────

    /// <summary>HomeText is set from the IHomeContentProvider on construction.</summary>
    [Fact]
    public void HomeText_SetFromProvider()
    {
        ContentAreaViewModel vm = Build(out _, "WELCOME");
        Assert.Equal("WELCOME", vm.HomeText);
    }

    // ── ShowHome ──────────────────────────────────────────────────────────────

    /// <summary>ShowHome sets ActiveContent to null and IsHomeVisible to true.</summary>
    [Fact]
    public void ShowHome_SetsActiveContentNullAndIsHomeVisible()
    {
        ContentAreaViewModel vm = Build(out _);
        vm.ShowEntry(new object());

        vm.ShowHome();

        Assert.Null(vm.ActiveContent);
        Assert.True(vm.IsHomeVisible);
    }

    // ── ShowEntry(object) ─────────────────────────────────────────────────────

    /// <summary>ShowEntry(object) sets ActiveContent and clears IsHomeVisible.</summary>
    [Fact]
    public void ShowEntry_Object_SetsActiveContentAndClearsIsHomeVisible()
    {
        ContentAreaViewModel vm = Build(out _);
        object entryVm = new();

        vm.ShowEntry(entryVm);

        Assert.Same(entryVm, vm.ActiveContent);
        Assert.False(vm.IsHomeVisible);
    }

    /// <summary>Initial state after construction is home screen visible.</summary>
    [Fact]
    public void InitialState_IsHomeVisible()
    {
        ContentAreaViewModel vm = Build(out _);
        Assert.True(vm.IsHomeVisible);
        Assert.Null(vm.ActiveContent);
    }

    // ── ShowEntry(EntryItemViewModel) ────────────────────────────────────────

    /// <summary>Opening an Outbox message entry looks up the outbound-scoped record, disambiguating from any same-MessageId Inbox record.</summary>
    [Fact]
    public async Task ShowEntry_OutboundMessage_LooksUpOutboundRecord()
    {
        Mock<IHomeContentProvider> home = new();
        Mock<IEntryService> entry = new();
        Mock<IMessageRepository> messages = new();
        Mock<IDraftRepository> drafts = new();
        Mock<INoteRepository> notes = new();
        Mock<IActivityLogRepository> activityLogs = new();
        ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
        MessageEntity outboundEntity = new() { MessageId = "MSG1", Message = new TestMessage(), IsOutbound = true };
        messages.Setup(m => m.Get("MSG1", true)).ReturnsAsync(outboundEntity);
        ContentAreaViewModel vm = new(home.Object, entry.Object, new FakeServiceConnection(), messages.Object,
            drafts.Object, notes.Object, activityLogs.Object, Format, loggerFactory);
        EntryItemViewModel item = new("MSG1", "Title", EntryType.Message, DateTime.UtcNow, isOutboundMessage: true);

        await vm.ShowEntry(item);

        messages.Verify(m => m.Get("MSG1", true), Times.Once);
        Assert.IsType<MessageViewModel>(vm.ActiveContent);
    }

    /// <summary>Opening an Inbox message entry looks up the inbound-scoped record, disambiguating from any same-MessageId Outbox record.</summary>
    [Fact]
    public async Task ShowEntry_InboundMessage_LooksUpInboundRecord()
    {
        Mock<IHomeContentProvider> home = new();
        Mock<IEntryService> entry = new();
        Mock<IMessageRepository> messages = new();
        Mock<IDraftRepository> drafts = new();
        Mock<INoteRepository> notes = new();
        Mock<IActivityLogRepository> activityLogs = new();
        ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
        MessageEntity inboundEntity = new() { MessageId = "MSG1", Message = new TestMessage(), IsOutbound = false };
        messages.Setup(m => m.Get("MSG1", false)).ReturnsAsync(inboundEntity);
        ContentAreaViewModel vm = new(home.Object, entry.Object, new FakeServiceConnection(), messages.Object,
            drafts.Object, notes.Object, activityLogs.Object, Format, loggerFactory);
        EntryItemViewModel item = new("MSG1", "Title", EntryType.Message, DateTime.UtcNow);

        await vm.ShowEntry(item);

        messages.Verify(m => m.Get("MSG1", false), Times.Once);
        Assert.IsType<MessageViewModel>(vm.ActiveContent);
    }

    // ── DeliveryStatusChanged ─────────────────────────────────────────────────

    /// <summary>DeliveryStatusChanged event calls UpdateDeliveryStatus when active content matches the message ID.</summary>
    [Fact]
    public async Task DeliveryStatusChanged_MatchingActiveMessage_CallsUpdateDeliveryStatus()
    {
        ContentAreaViewModel vm = Build(out FakeServiceConnection conn);
        Mock<IMessageViewModel> msgVm = new();
        msgVm.Setup(m => m.MessageId).Returns("MSG-1");
        vm.ShowEntry(msgVm.Object);

        DeliveryStatusChangedEvent evt = new()
        {
            MessageId = "MSG-1",
            SiteName = "DEST",
            Status = DestinationStatus.Confirmed
        };
        await conn.RaiseDeliveryStatusChanged(evt);

        msgVm.Verify(m => m.UpdateDeliveryStatus("DEST", DestinationStatus.Confirmed), Times.Once);
    }

    /// <summary>DeliveryStatusChanged with a different message ID does not call UpdateDeliveryStatus.</summary>
    [Fact]
    public async Task DeliveryStatusChanged_NonMatchingMessageId_DoesNotUpdate()
    {
        ContentAreaViewModel vm = Build(out FakeServiceConnection conn);
        Mock<IMessageViewModel> msgVm = new();
        msgVm.Setup(m => m.MessageId).Returns("MSG-1");
        vm.ShowEntry(msgVm.Object);

        DeliveryStatusChangedEvent evt = new()
        {
            MessageId = "MSG-OTHER",
            SiteName = "DEST",
            Status = DestinationStatus.Confirmed
        };
        await conn.RaiseDeliveryStatusChanged(evt);

        msgVm.Verify(m => m.UpdateDeliveryStatus(It.IsAny<string>(), It.IsAny<DestinationStatus>()), Times.Never);
    }

    /// <summary>DeliveryStatusChanged when ActiveContent is not IMessageViewModel is a no-op.</summary>
    [Fact]
    public async Task DeliveryStatusChanged_NonMessageViewModel_IsNoOp()
    {
        ContentAreaViewModel vm = Build(out FakeServiceConnection conn);
        vm.ShowEntry(new object());

        DeliveryStatusChangedEvent evt = new()
        {
            MessageId = "MSG-1",
            SiteName = "DEST",
            Status = DestinationStatus.Confirmed
        };
        await conn.RaiseDeliveryStatusChanged(evt);
    }
}
