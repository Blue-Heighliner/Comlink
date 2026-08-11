namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="EntryBarViewModel"/>.</summary>
public sealed class EntryBarViewModelTests
{
    private static readonly IMessageFormat Format = new TestMessageFormat();

    private static FolderItemViewModel MakeFolder(string id, FolderType type)
        => new(id, type.ToString(), type, null);

    private static MessageEntity MakeMessage(string id = "MSG1", string fromUser = "ALPHA", string subject = "Hello")
    {
        object message = Format.CreateMessage();
        Format.SetMessageId(message, id);
        Format.SetFromUser(message, fromUser);
        Format.SetSubject(message, subject);
        Format.SetBody(message, "body");
        return new MessageEntity
        {
            MessageId = id,
            Message = message,
            ReceivedAt = DateTime.UtcNow,
            DeliveryStatuses = []
        };
    }

    private static DraftEntity MakeDraft(string subject = "Draft subject")
        => new()
        {
            Id = new ObjectId(),
            Subject = subject,
            Body = "",
            FolderId = "root-drafts",
            ModifiedAt = DateTime.UtcNow,
            Addresses = []
        };

    private static NoteEntity MakeNote(string body = "Note text")
        => new()
        {
            Id = new ObjectId(),
            Body = body,
            FolderId = "root-notes",
            ModifiedAt = DateTime.UtcNow
        };

    // ── LoadFolder – Inbox ────────────────────────────────────────────────────

    /// <summary>LoadFolder(Inbox) populates Entries from GetMessages.</summary>
    [Fact]
    public async Task LoadFolder_Inbox_PopulatesEntriesFromMessages()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, Format);
        FolderItemViewModel inbox = MakeFolder("root-inbox", FolderType.Inbox);

        await vm.LoadFolder(inbox);

        Assert.Single(vm.Entries);
        Assert.Equal("M1", vm.Entries[0].Id);
    }

    // ── LoadFolder – Drafts ───────────────────────────────────────────────────

    /// <summary>LoadFolder(Drafts) populates Entries from GetDrafts and enables sort toggle.</summary>
    [Fact]
    public async Task LoadFolder_Drafts_PopulatesEntriesAndShowsSortToggle()
    {
        Mock<IEntryService> svc = new();
        DraftEntity draft = MakeDraft("My draft");
        svc.Setup(s => s.GetDrafts(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
           .ReturnsAsync((Items: new List<DraftEntity> { draft }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, Format);
        FolderItemViewModel drafts = MakeFolder("root-drafts", FolderType.Drafts);

        await vm.LoadFolder(drafts);

        Assert.True(vm.ShowSortToggle);
        Assert.Single(vm.Entries);
        Assert.Equal(draft.Id.ToString(), vm.Entries[0].Id);
    }

    // ── LoadFolder – Notes ────────────────────────────────────────────────────

    /// <summary>LoadFolder(Notes) uses the first line of body as the title.</summary>
    [Fact]
    public async Task LoadFolder_Notes_UsesFirstLineAsTitle()
    {
        Mock<IEntryService> svc = new();
        NoteEntity note = MakeNote("Line one\nLine two");
        svc.Setup(s => s.GetNotes(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
           .ReturnsAsync((Items: new List<NoteEntity> { note }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, Format);

        await vm.LoadFolder(MakeFolder("root-notes", FolderType.Notes));

        Assert.Equal("Line one", vm.Entries[0].Title);
    }

    // ── SelectEntry ───────────────────────────────────────────────────────────

    /// <summary>SelectEntry fires EntrySelected and updates SelectedEntry.</summary>
    [Fact]
    public async Task SelectEntry_FiresEventAndUpdatesSelection()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, Format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        EntryItemViewModel entry = vm.Entries[0];
        EntryItemViewModel? received = null;
        vm.EntrySelected += e => received = e;

        vm.SelectEntry(entry);

        Assert.Same(entry, vm.SelectedEntry);
        Assert.Same(entry, received);
        Assert.True(entry.IsSelected);
    }

    /// <summary>Selecting a new entry deselects the previously selected one.</summary>
    [Fact]
    public async Task SelectEntry_DeselectedPrevious()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1"), MakeMessage("M2") }, Total: 2));
        EntryBarViewModel vm = new(svc.Object, Format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        EntryItemViewModel first = vm.Entries[0];
        vm.SelectEntry(first);

        vm.SelectEntry(vm.Entries[1]);

        Assert.False(first.IsSelected);
    }

    // ── LoadFolder – Outbox ───────────────────────────────────────────────────

    /// <summary>LoadFolder(Outbox) marks entries as outbound messages, while LoadFolder(Inbox) does not.</summary>
    [Fact]
    public async Task LoadFolder_Outbox_MarksEntriesAsOutboundMessage()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, Format);

        await vm.LoadFolder(MakeFolder("root-outbox", FolderType.Outbox));
        Assert.True(vm.Entries[0].IsOutboundMessage);

        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        Assert.False(vm.Entries[0].IsOutboundMessage);
    }

    // ── DeleteEntry – outbound disambiguation ────────────────────────────────

    /// <summary>DeleteEntry passes the entry's IsOutboundMessage flag through to the service so self-addressed duplicates are disambiguated.</summary>
    [Fact]
    public async Task DeleteEntry_OutboundMessage_PassesFlagToService()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1") }, Total: 1));
        svc.Setup(s => s.DeleteEntry(It.IsAny<string>(), It.IsAny<EntryType>(), It.IsAny<bool>())).Returns(Task.CompletedTask);
        EntryBarViewModel vm = new(svc.Object, Format);
        await vm.LoadFolder(MakeFolder("root-outbox", FolderType.Outbox));
        EntryItemViewModel entry = vm.Entries[0];

        await vm.DeleteEntry(entry);

        svc.Verify(s => s.DeleteEntry("M1", EntryType.Message, true), Times.Once);
    }

    // ── UpdateEntryStatus ─────────────────────────────────────────────────────

    /// <summary>UpdateEntryStatus sets OverallStatus on the matching message entry.</summary>
    [Fact]
    public async Task UpdateEntryStatus_SetsStatusOnMatchingEntry()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("MSG42") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, Format);
        await vm.LoadFolder(MakeFolder("root-outbox", FolderType.Outbox));

        await vm.UpdateEntryStatus("MSG42", DestinationStatus.Confirmed);

        Assert.Equal(DestinationStatus.Confirmed, vm.Entries[0].OverallStatus);
    }

    /// <summary>UpdateEntryStatus for an unknown ID does not throw.</summary>
    [Fact]
    public async Task UpdateEntryStatus_UnknownId_DoesNotThrow()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        EntryBarViewModel vm = new(svc.Object, Format);
        await vm.LoadFolder(MakeFolder("root-outbox", FolderType.Outbox));

        Exception? ex = await Record.ExceptionAsync(() => vm.UpdateEntryStatus("UNKNOWN", DestinationStatus.Failed));
        Assert.Null(ex);
    }

    // ── DeleteEntry ───────────────────────────────────────────────────────────

    /// <summary>DeleteEntry calls the service and removes the item from Entries.</summary>
    [Fact]
    public async Task DeleteEntry_CallsServiceAndRemovesFromList()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1") }, Total: 1));
        svc.Setup(s => s.DeleteEntry(It.IsAny<string>(), It.IsAny<EntryType>())).Returns(Task.CompletedTask);
        EntryBarViewModel vm = new(svc.Object, Format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        EntryItemViewModel entry = vm.Entries[0];

        await vm.DeleteEntry(entry);

        Assert.Empty(vm.Entries);
        svc.Verify(s => s.DeleteEntry("M1", EntryType.Message), Times.Once);
    }

    // ── SetPendingSelectId ────────────────────────────────────────────────────

    /// <summary>SetPendingSelectId causes the matching entry to be auto-selected after the next refresh.</summary>
    [Fact]
    public async Task SetPendingSelectId_AutoSelectsAfterRefresh()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetDrafts(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
           .ReturnsAsync((Items: new List<DraftEntity> { MakeDraft() }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, Format);
        await vm.LoadFolder(MakeFolder("root-drafts", FolderType.Drafts));
        string id = vm.Entries[0].Id;

        vm.SetPendingSelectId(id);
        await vm.Refresh();

        Assert.NotNull(vm.SelectedEntry);
        Assert.Equal(id, vm.SelectedEntry.Id);
    }
}
