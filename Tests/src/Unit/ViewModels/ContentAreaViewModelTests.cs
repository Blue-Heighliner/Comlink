namespace BlueHeighliner.Comlink.Tests.Unit.ViewModels;

/// <summary>Unit tests for <see cref="ContentAreaViewModel"/>.</summary>
public sealed class ContentAreaViewModelTests
{
    private sealed class FakeServiceConnection : IServiceConnection
    {
#pragma warning disable CS0067
        public event Func<MessageReceivedEvent, Task>? MessageReceived;
#pragma warning restore CS0067
        public event Func<DeliveryStatusChangedEvent, Task>? DeliveryStatusChanged;

        public bool MarkMessageReadResult { get; set; } = true;
        public List<string> MarkMessageReadCalls { get; } = [];

        public Task Connect(CancellationToken cancellation = default) => Task.CompletedTask;
        public Task<UserInfo?> GetUserInfo(CancellationToken cancellation = default) => Task.FromResult<UserInfo?>(null);
        public Task<List<string>> GetUserNames(CancellationToken cancellation = default) => Task.FromResult(new List<string>());
        public Task<UserInfo?> InstallUser(string userCode, CancellationToken cancellation = default) => Task.FromResult<UserInfo?>(null);
        public Task<SendMessageResult?> SendMessage(string subject, string body, List<AddressRequest> addresses, bool isAlert = false, int priority = 0, string tag = "", CancellationToken cancellation = default) => Task.FromResult<SendMessageResult?>(null);

        public Task<bool> MarkMessageRead(string messageId, CancellationToken cancellation = default)
        {
            MarkMessageReadCalls.Add(messageId);
            return Task.FromResult(MarkMessageReadResult);
        }

        public async Task RaiseDeliveryStatusChanged(DeliveryStatusChangedEvent evt)
        {
            if (DeliveryStatusChanged is not null) { await DeliveryStatusChanged(evt); }
        }
    }

    private static IEngineController MakeEngineController(string homeText = "HOME")
    {
        Mock<TestEngineController> mock = new() { CallBase = true };
        mock.Setup(h => h.HomeText).Returns(homeText);
        mock.Setup(p => p.Priorities).Returns([new MessagePriorityOption { Name = "Normal", Value = 0 }]);
        mock.Setup(t => t.TagsEnabled).Returns(true);
        mock.Setup(t => t.TagLabel).Returns("Tag");
        mock.Setup(p => p.BlockedCombinations).Returns([]);
        mock.Setup(a => a.AlertLabel).Returns("ALERT");
        mock.Setup(a => a.ComposeAlertsEnabled).Returns(true);
        return mock.Object;
    }

    private static ContentAreaViewModel Build(out FakeServiceConnection connection, string homeText = "HOME")
    {
        connection = new FakeServiceConnection();
        Mock<IEntryService> entry = new();
        Mock<IMessageRepository> messages = new();
        Mock<IDraftRepository> drafts = new();
        Mock<INoteRepository> notes = new();
        Mock<IActivityLogRepository> activityLogs = new();
        ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
        return new ContentAreaViewModel(MakeEngineController(homeText), entry.Object, connection, messages.Object,
            drafts.Object, notes.Object, activityLogs.Object, loggerFactory);
    }

    private static ContentAreaViewModel BuildWithEditors(out Mock<INoteRepository> notes, out Mock<IDraftRepository> drafts, out Mock<IEntryService> entry)
    {
        entry = new Mock<IEntryService>();
        notes = new Mock<INoteRepository>();
        drafts = new Mock<IDraftRepository>();
        return new ContentAreaViewModel(MakeEngineController(), entry.Object, new FakeServiceConnection(), new Mock<IMessageRepository>().Object,
            drafts.Object, notes.Object, new Mock<IActivityLogRepository>().Object, LoggerFactory.Create(_ => { }));
    }

    /// <summary>Deleting the open note from its editor deletes it, returns to the home screen, and tells listeners so they can refresh the list.</summary>
    [Fact]
    public async Task OpenNote_DeletedFromEditor_ShowsHomeAndRaisesEntryDeleted()
    {
        ContentAreaViewModel vm = BuildWithEditors(out Mock<INoteRepository> notes, out _, out Mock<IEntryService> entry);
        NoteEntity note = new() { Id = new ObjectId(), Body = "B", FolderId = "root-notes", ModifiedAt = DateTime.UtcNow };
        notes.Setup(n => n.Get(note.Id)).ReturnsAsync(note);
        int raised = 0;
        vm.EntryDeleted += () => { raised++; return Task.CompletedTask; };
        await vm.ShowEntry(new EntryItemViewModel(note.Id.ToString(), "N", EntryType.Note, DateTime.UtcNow));
        NoteViewModel editor = Assert.IsType<NoteViewModel>(vm.ActiveContent);
        Assert.True(editor.CanDelete);

        await editor.DeleteCommand.ExecuteAsync(null);
        Assert.NotNull(vm.ActiveContent);
        await editor.DeleteCommand.ExecuteAsync(null);

        entry.Verify(e => e.DeleteEntry(note.Id.ToString(), EntryType.Note, false), Times.Once);
        Assert.Null(vm.ActiveContent);
        Assert.True(vm.IsHomeVisible);
        Assert.Equal(1, raised);
    }

    /// <summary>Deleting the open draft from its editor behaves the same way.</summary>
    [Fact]
    public async Task OpenDraft_DeletedFromEditor_ShowsHomeAndRaisesEntryDeleted()
    {
        ContentAreaViewModel vm = BuildWithEditors(out _, out Mock<IDraftRepository> drafts, out Mock<IEntryService> entry);
        DraftEntity draft = new() { Id = new ObjectId(), Subject = "S", Body = "B", Addresses = [], FolderId = "root-drafts" };
        drafts.Setup(d => d.Get(draft.Id)).ReturnsAsync(draft);
        int raised = 0;
        vm.EntryDeleted += () => { raised++; return Task.CompletedTask; };
        await vm.ShowEntry(new EntryItemViewModel(draft.Id.ToString(), "D", EntryType.Draft, DateTime.UtcNow));
        DraftViewModel editor = Assert.IsType<DraftViewModel>(vm.ActiveContent);

        await editor.DeleteCommand.ExecuteAsync(null);
        await editor.DeleteCommand.ExecuteAsync(null);

        entry.Verify(e => e.DeleteEntry(draft.Id.ToString(), EntryType.Draft, false), Times.Once);
        Assert.Null(vm.ActiveContent);
        Assert.Equal(1, raised);
    }

    /// <summary>A draft opened from the list gets its body document from the same factory as a new draft, so the editor can bind it; a plain string document there crashed the Client UI.</summary>
    [Fact]
    public async Task OpenDraft_UsesBodyDocumentFromFactory()
    {
        Mock<IEntryService> entry = new();
        Mock<IDraftRepository> drafts = new();
        StringBodyDocument document = new();
        Mock<IBodyDocumentFactory> factory = new();
        factory.Setup(f => f.Create()).Returns(document);
        ContentAreaViewModel vm = new(MakeEngineController(), entry.Object, new FakeServiceConnection(), new Mock<IMessageRepository>().Object,
            drafts.Object, new Mock<INoteRepository>().Object, new Mock<IActivityLogRepository>().Object, LoggerFactory.Create(_ => { }), factory.Object);
        DraftEntity draft = new() { Id = new ObjectId(), Subject = "S", Body = "B", Addresses = [], FolderId = "root-drafts" };
        drafts.Setup(d => d.Get(draft.Id)).ReturnsAsync(draft);

        await vm.ShowEntry(new EntryItemViewModel(draft.Id.ToString(), "D", EntryType.Draft, DateTime.UtcNow));

        Assert.Same(document, Assert.IsType<DraftViewModel>(vm.ActiveContent).BodyDocument);
        factory.Verify(f => f.Create(), Times.Once);
    }

    /// <summary>HomeText is set from IEngineController on construction.</summary>
    [Fact]
    public void HomeText_SetFromProvider()
    {
        ContentAreaViewModel vm = Build(out _, "WELCOME");
        Assert.Equal("WELCOME", vm.HomeText);
    }

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

    /// <summary>Opening an Outbox message entry looks up the outbound-scoped record, disambiguating from any same-MessageId Inbox record.</summary>
    [Fact]
    public async Task ShowEntry_OutboundMessage_LooksUpOutboundRecord()
    {
        Mock<IEntryService> entry = new();
        Mock<IMessageRepository> messages = new();
        Mock<IDraftRepository> drafts = new();
        Mock<INoteRepository> notes = new();
        Mock<IActivityLogRepository> activityLogs = new();
        ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
        MessageEntity outboundEntity = new() { MessageId = "MSG1", Message = new TestMessage(), IsOutbound = true };
        messages.Setup(m => m.Get("MSG1", true)).ReturnsAsync(outboundEntity);
        ContentAreaViewModel vm = new(MakeEngineController(), entry.Object, new FakeServiceConnection(), messages.Object,
            drafts.Object, notes.Object, activityLogs.Object, loggerFactory);
        EntryItemViewModel item = new("MSG1", "Title", EntryType.Message, DateTime.UtcNow, isOutboundMessage: true);

        await vm.ShowEntry(item);

        messages.Verify(m => m.Get("MSG1", true), Times.Once);
        Assert.IsType<MessageViewModel>(vm.ActiveContent);
    }

    /// <summary>Opening an Inbox message entry looks up the inbound-scoped record, disambiguating from any same-MessageId Outbox record.</summary>
    [Fact]
    public async Task ShowEntry_InboundMessage_LooksUpInboundRecord()
    {
        Mock<IEntryService> entry = new();
        Mock<IMessageRepository> messages = new();
        Mock<IDraftRepository> drafts = new();
        Mock<INoteRepository> notes = new();
        Mock<IActivityLogRepository> activityLogs = new();
        ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
        MessageEntity inboundEntity = new() { MessageId = "MSG1", Message = new TestMessage(), IsOutbound = false };
        messages.Setup(m => m.Get("MSG1", false)).ReturnsAsync(inboundEntity);
        ContentAreaViewModel vm = new(MakeEngineController(), entry.Object, new FakeServiceConnection(), messages.Object,
            drafts.Object, notes.Object, activityLogs.Object, loggerFactory);
        EntryItemViewModel item = new("MSG1", "Title", EntryType.Message, DateTime.UtcNow);

        await vm.ShowEntry(item);

        messages.Verify(m => m.Get("MSG1", false), Times.Once);
        Assert.IsType<MessageViewModel>(vm.ActiveContent);
    }

    /// <summary>Opening an unread Inbox message marks it read via the connection and reflects Read status on the ViewModel.</summary>
    [Fact]
    public async Task ShowEntry_UnreadInboundMessage_MarksRead()
    {
        Mock<IEntryService> entry = new();
        Mock<IMessageRepository> messages = new();
        Mock<IDraftRepository> drafts = new();
        Mock<INoteRepository> notes = new();
        Mock<IActivityLogRepository> activityLogs = new();
        ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
        MessageEntity inboundEntity = new() { MessageId = "MSG1", Message = new TestMessage(), IsOutbound = false, ReadStatus = DestinationStatus.Received };
        messages.Setup(m => m.Get("MSG1", false)).ReturnsAsync(inboundEntity);
        FakeServiceConnection connection = new();
        ContentAreaViewModel vm = new(MakeEngineController(), entry.Object, connection, messages.Object,
            drafts.Object, notes.Object, activityLogs.Object, loggerFactory);
        EntryItemViewModel item = new("MSG1", "Title", EntryType.Message, DateTime.UtcNow);

        await vm.ShowEntry(item);

        Assert.Equal(["MSG1"], connection.MarkMessageReadCalls);
        IMessageViewModel msgVm = Assert.IsType<MessageViewModel>(vm.ActiveContent);
        Assert.Equal(DestinationStatus.Read, msgVm.ReadStatus);
    }

    /// <summary>Opening an already-read Inbox message does not call MarkMessageRead again.</summary>
    [Fact]
    public async Task ShowEntry_AlreadyReadInboundMessage_DoesNotMarkReadAgain()
    {
        Mock<IEntryService> entry = new();
        Mock<IMessageRepository> messages = new();
        Mock<IDraftRepository> drafts = new();
        Mock<INoteRepository> notes = new();
        Mock<IActivityLogRepository> activityLogs = new();
        ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
        MessageEntity inboundEntity = new() { MessageId = "MSG1", Message = new TestMessage(), IsOutbound = false, ReadStatus = DestinationStatus.Read };
        messages.Setup(m => m.Get("MSG1", false)).ReturnsAsync(inboundEntity);
        FakeServiceConnection connection = new();
        ContentAreaViewModel vm = new(MakeEngineController(), entry.Object, connection, messages.Object,
            drafts.Object, notes.Object, activityLogs.Object, loggerFactory);
        EntryItemViewModel item = new("MSG1", "Title", EntryType.Message, DateTime.UtcNow);

        await vm.ShowEntry(item);

        Assert.Empty(connection.MarkMessageReadCalls);
    }

    /// <summary>Opening an Outbox message never calls MarkMessageRead, which only applies to Inbox records.</summary>
    [Fact]
    public async Task ShowEntry_OutboundMessage_DoesNotCallMarkMessageRead()
    {
        Mock<IEntryService> entry = new();
        Mock<IMessageRepository> messages = new();
        Mock<IDraftRepository> drafts = new();
        Mock<INoteRepository> notes = new();
        Mock<IActivityLogRepository> activityLogs = new();
        ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
        MessageEntity outboundEntity = new() { MessageId = "MSG1", Message = new TestMessage(), IsOutbound = true };
        messages.Setup(m => m.Get("MSG1", true)).ReturnsAsync(outboundEntity);
        FakeServiceConnection connection = new();
        ContentAreaViewModel vm = new(MakeEngineController(), entry.Object, connection, messages.Object,
            drafts.Object, notes.Object, activityLogs.Object, loggerFactory);
        EntryItemViewModel item = new("MSG1", "Title", EntryType.Message, DateTime.UtcNow, isOutboundMessage: true);

        await vm.ShowEntry(item);

        Assert.Empty(connection.MarkMessageReadCalls);
    }

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
            UserName = "DEST",
            Status = DestinationStatus.Confirmed
        };
        await conn.RaiseDeliveryStatusChanged(evt);

        msgVm.Verify(m => m.UpdateDeliveryStatus("DEST", DestinationStatus.Confirmed), Times.Once);
    }

    /// <summary>A local read-status notification (empty UserName) sets ReadStatus rather than calling UpdateDeliveryStatus.</summary>
    [Fact]
    public async Task DeliveryStatusChanged_EmptyUserName_SetsReadStatusInstead()
    {
        ContentAreaViewModel vm = Build(out FakeServiceConnection conn);
        Mock<IMessageViewModel> msgVm = new();
        msgVm.Setup(m => m.MessageId).Returns("MSG-1");
        msgVm.SetupProperty(m => m.ReadStatus);
        vm.ShowEntry(msgVm.Object);

        DeliveryStatusChangedEvent evt = new()
        {
            MessageId = "MSG-1",
            UserName = "",
            Status = DestinationStatus.Read
        };
        await conn.RaiseDeliveryStatusChanged(evt);

        Assert.Equal(DestinationStatus.Read, msgVm.Object.ReadStatus);
        msgVm.Verify(m => m.UpdateDeliveryStatus(It.IsAny<string>(), It.IsAny<DestinationStatus>()), Times.Never);
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
            UserName = "DEST",
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
            UserName = "DEST",
            Status = DestinationStatus.Confirmed
        };
        await conn.RaiseDeliveryStatusChanged(evt);
    }
}
