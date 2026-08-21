namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="EntryBarViewModel"/>.</summary>
public sealed class EntryBarViewModelTests
{
    private static readonly IEngineController format = new TestEngineController();

    private static FolderItemViewModel MakeFolder(string id, FolderType type)
        => new(id, type.ToString(), type, null);

    private static MessageEntity MakeMessage(string id = "MSG1", string fromUser = "ALPHA", string subject = "Hello", int priority = 0, string tag = "")
    {
        object message = format.CreateMessage();
        format.SetMessageId(message, id);
        format.SetFromUser(message, fromUser);
        format.SetSubject(message, subject);
        format.SetBody(message, "body");
        format.SetPriority(message, priority);
        format.SetTag(message, tag);
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

    /// <summary>LoadFolder(Inbox) populates Entries from GetMessages.</summary>
    [Fact]
    public async Task LoadFolder_Inbox_PopulatesEntriesFromMessages()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, format);
        FolderItemViewModel inbox = MakeFolder("root-inbox", FolderType.Inbox);

        await vm.LoadFolder(inbox);

        Assert.Single(vm.Entries);
        Assert.Equal("M1", vm.Entries[0].Id);
    }

    /// <summary>LoadFolder(Drafts) populates Entries from GetDrafts and enables sort toggle.</summary>
    [Fact]
    public async Task LoadFolder_Drafts_PopulatesEntriesAndShowsSortToggle()
    {
        Mock<IEntryService> svc = new();
        DraftEntity draft = MakeDraft("My draft");
        svc.Setup(s => s.GetDrafts(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
           .ReturnsAsync((Items: new List<DraftEntity> { draft }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, format);
        FolderItemViewModel drafts = MakeFolder("root-drafts", FolderType.Drafts);

        await vm.LoadFolder(drafts);

        Assert.True(vm.ShowSortToggle);
        Assert.Single(vm.Entries);
        Assert.Equal(draft.Id.ToString(), vm.Entries[0].Id);
    }

    /// <summary>LoadFolder(Notes) uses the first line of body as the title.</summary>
    [Fact]
    public async Task LoadFolder_Notes_UsesFirstLineAsTitle()
    {
        Mock<IEntryService> svc = new();
        NoteEntity note = MakeNote("Line one\nLine two");
        svc.Setup(s => s.GetNotes(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
           .ReturnsAsync((Items: new List<NoteEntity> { note }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, format);

        await vm.LoadFolder(MakeFolder("root-notes", FolderType.Notes));

        Assert.Equal("Line one", vm.Entries[0].Title);
    }

    /// <summary>SelectEntry fires EntriesSelected with a single-item list and updates SelectedEntry.</summary>
    [Fact]
    public async Task SelectEntry_FiresEventAndUpdatesSelection()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        EntryItemViewModel entry = vm.Entries[0];
        IReadOnlyList<EntryItemViewModel>? received = null;
        vm.EntriesSelected += e => received = e;

        vm.SelectEntry(entry);

        Assert.Same(entry, vm.SelectedEntry);
        Assert.Equal([entry], received);
        Assert.True(entry.IsSelected);
    }

    /// <summary>Selecting a new entry deselects the previously selected one.</summary>
    [Fact]
    public async Task SelectEntry_DeselectedPrevious()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1"), MakeMessage("M2") }, Total: 2));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        EntryItemViewModel first = vm.Entries[0];
        vm.SelectEntry(first);

        vm.SelectEntry(vm.Entries[1]);

        Assert.False(first.IsSelected);
    }

    /// <summary>Selecting a new entry deselects every previously multi-selected entry, not just SelectedEntry.</summary>
    [Fact]
    public async Task SelectEntry_DeselectedAllPreviousMultiSelection()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1"), MakeMessage("M2"), MakeMessage("M3") }, Total: 3));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        vm.SelectEntries([vm.Entries[0], vm.Entries[1]], []);

        vm.SelectEntry(vm.Entries[2]);

        Assert.False(vm.Entries[0].IsSelected);
        Assert.False(vm.Entries[1].IsSelected);
        Assert.True(vm.Entries[2].IsSelected);
    }

    /// <summary>SelectEntries marks every added entry selected and fires EntriesSelected with the added list.</summary>
    [Fact]
    public async Task SelectEntries_MarksAddedSelectedAndFiresEvent()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1"), MakeMessage("M2"), MakeMessage("M3") }, Total: 3));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        IReadOnlyList<EntryItemViewModel>? received = null;
        vm.EntriesSelected += e => received = e;

        vm.SelectEntries([vm.Entries[0], vm.Entries[1], vm.Entries[2]], []);

        Assert.True(vm.Entries[0].IsSelected);
        Assert.True(vm.Entries[1].IsSelected);
        Assert.True(vm.Entries[2].IsSelected);
        Assert.Equal([vm.Entries[0], vm.Entries[1], vm.Entries[2]], received);
    }

    /// <summary>SelectEntries deselects every removed entry (e.g. a ctrl-click toggle-off).</summary>
    [Fact]
    public async Task SelectEntries_DeselectsRemoved()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1"), MakeMessage("M2") }, Total: 2));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        vm.SelectEntries([vm.Entries[0], vm.Entries[1]], []);

        vm.SelectEntries([], [vm.Entries[0]]);

        Assert.False(vm.Entries[0].IsSelected);
        Assert.True(vm.Entries[1].IsSelected);
    }

    /// <summary>SelectEntries with no added entries does not raise EntriesSelected.</summary>
    [Fact]
    public async Task SelectEntries_NoAdded_DoesNotRaiseEvent()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        vm.SelectEntries([vm.Entries[0]], []);
        bool raised = false;
        vm.EntriesSelected += _ => raised = true;

        vm.SelectEntries([], [vm.Entries[0]]);

        Assert.False(raised);
    }

    /// <summary>
    /// SelectEntries never assigns SelectedEntry: it reacts to the View's own SelectionChanged, and
    /// SelectedEntry drives a OneWay binding back into that same ListBox's SelectedItem — writing it here
    /// would collapse a ctrl/shift multi-selection down to a single item (regression test for the bug
    /// where "click entry A, then ctrl-click entry B" ended up with only B selected).
    /// </summary>
    [Fact]
    public async Task SelectEntries_DoesNotAssignSelectedEntry()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1"), MakeMessage("M2") }, Total: 2));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        vm.SelectEntries([vm.Entries[0], vm.Entries[1]], []);

        Assert.Null(vm.SelectedEntry);
    }

    /// <summary>SelectEntries leaves a pre-existing SelectedEntry (e.g. from a prior programmatic SelectEntry) untouched.</summary>
    [Fact]
    public async Task SelectEntries_LeavesExistingSelectedEntryUntouched()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1"), MakeMessage("M2"), MakeMessage("M3") }, Total: 3));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        vm.SelectEntry(vm.Entries[0]);

        vm.SelectEntries([vm.Entries[1], vm.Entries[2]], []);

        Assert.Same(vm.Entries[0], vm.SelectedEntry);
    }

    /// <summary>Both entries from a ctrl-click-style selection (A then A+B, neither removed) remain marked selected.</summary>
    [Fact]
    public async Task SelectEntries_CtrlClickAfterPlainClick_BothRemainSelected()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1"), MakeMessage("M2") }, Total: 2));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        // Plain click on entry A (as the View's SelectionChanged would report it).
        vm.SelectEntries([vm.Entries[0]], []);
        // Ctrl-click on entry B: Avalonia's native selection adds B without removing A.
        vm.SelectEntries([vm.Entries[1]], []);

        Assert.True(vm.Entries[0].IsSelected);
        Assert.True(vm.Entries[1].IsSelected);
    }

    /// <summary>DeselectEntry clears SelectedEntry and unmarks the entry's IsSelected flag.</summary>
    [Fact]
    public async Task DeselectEntry_ClearsSelectionAndFlag()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        EntryItemViewModel entry = vm.Entries[0];
        vm.SelectEntry(entry);

        vm.DeselectEntry();

        Assert.Null(vm.SelectedEntry);
        Assert.False(entry.IsSelected);
    }

    /// <summary>DeselectEntry clears every multi-selected entry, not just SelectedEntry.</summary>
    [Fact]
    public async Task DeselectEntry_ClearsAllMultiSelectedEntries()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1"), MakeMessage("M2") }, Total: 2));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        vm.SelectEntries([vm.Entries[0], vm.Entries[1]], []);

        vm.DeselectEntry();

        Assert.False(vm.Entries[0].IsSelected);
        Assert.False(vm.Entries[1].IsSelected);
        Assert.Null(vm.SelectedEntry);
    }

    /// <summary>DeselectEntry does not raise EntriesSelected.</summary>
    [Fact]
    public async Task DeselectEntry_DoesNotRaiseEntriesSelected()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        vm.SelectEntry(vm.Entries[0]);
        bool raised = false;
        vm.EntriesSelected += _ => raised = true;

        vm.DeselectEntry();

        Assert.False(raised);
    }

    /// <summary>DeselectEntry is a no-op when nothing is selected.</summary>
    [Fact]
    public void DeselectEntry_NothingSelected_IsNoOp()
    {
        EntryBarViewModel vm = new(new Mock<IEntryService>().Object, format);

        Exception? ex = Record.Exception(() => vm.DeselectEntry());

        Assert.Null(ex);
        Assert.Null(vm.SelectedEntry);
    }

    /// <summary>LoadFolder(Outbox) marks entries as outbound messages, while LoadFolder(Inbox) does not.</summary>
    [Fact]
    public async Task LoadFolder_Outbox_MarksEntriesAsOutboundMessage()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, format);

        await vm.LoadFolder(MakeFolder("root-outbox", FolderType.Outbox));
        Assert.True(vm.Entries[0].IsOutboundMessage);

        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        Assert.False(vm.Entries[0].IsOutboundMessage);
    }

    /// <summary>Inbox entries carry a PriorityText label resolved from the message's stored priority via IEngineController.</summary>
    [Fact]
    public async Task LoadFolder_Inbox_SetsPriorityTextFromProvider()
    {
        Mock<TestEngineController> priorityProvider = new() { CallBase = true };
        priorityProvider.Setup(p => p.Priorities).Returns([
            new MessagePriorityOption { Name = "Low", Value = 0 },
            new MessagePriorityOption { Name = "Medium", Value = 1 },
            new MessagePriorityOption { Name = "High", Value = 2 }
        ]);
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1", priority: 2) }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, priorityProvider.Object);

        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        Assert.Equal("High", vm.Entries[0].PriorityText);
    }

    /// <summary>Outbox entries carry a PriorityText label resolved the same way as Inbox entries.</summary>
    [Fact]
    public async Task LoadFolder_Outbox_SetsPriorityTextFromProvider()
    {
        Mock<TestEngineController> priorityProvider = new() { CallBase = true };
        priorityProvider.Setup(p => p.Priorities).Returns([
            new MessagePriorityOption { Name = "Low", Value = 0 },
            new MessagePriorityOption { Name = "Medium", Value = 1 },
            new MessagePriorityOption { Name = "High", Value = 2 }
        ]);
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1", priority: 1) }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, priorityProvider.Object);

        await vm.LoadFolder(MakeFolder("root-outbox", FolderType.Outbox));

        Assert.Equal("Medium", vm.Entries[0].PriorityText);
    }

    /// <summary>A stored priority value with no matching option falls back to its plain numeric string.</summary>
    [Fact]
    public async Task LoadFolder_Inbox_PriorityWithNoMatchingOption_FallsBackToNumber()
    {
        Mock<TestEngineController> priorityProvider = new() { CallBase = true };
        priorityProvider.Setup(p => p.Priorities).Returns([new MessagePriorityOption { Name = "Normal", Value = 0 }]);
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1", priority: 99) }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, priorityProvider.Object);

        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        Assert.Equal("99", vm.Entries[0].PriorityText);
    }

    /// <summary>Inbox entries carry a TagText label from the message's stored tag when tags are enabled.</summary>
    [Fact]
    public async Task LoadFolder_Inbox_TagsEnabled_SetsTagTextFromMessage()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1", tag: "URGENT") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, format);

        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        Assert.Equal("URGENT", vm.Entries[0].TagText);
    }

    /// <summary>Outbox entries carry a TagText label the same way as Inbox entries.</summary>
    [Fact]
    public async Task LoadFolder_Outbox_TagsEnabled_SetsTagTextFromMessage()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1", tag: "URGENT") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, format);

        await vm.LoadFolder(MakeFolder("root-outbox", FolderType.Outbox));

        Assert.Equal("URGENT", vm.Entries[0].TagText);
    }

    /// <summary>An empty stored tag yields a null TagText rather than an empty string.</summary>
    [Fact]
    public async Task LoadFolder_Inbox_EmptyTag_TagTextIsNull()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, format);

        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        Assert.Null(vm.Entries[0].TagText);
    }

    /// <summary>When tags are disabled, TagText is null even for a message with a stored tag.</summary>
    [Fact]
    public async Task LoadFolder_Inbox_TagsDisabled_TagTextIsNull()
    {
        Mock<TestEngineController> tagConfiguration = new() { CallBase = true };
        tagConfiguration.Setup(t => t.TagsEnabled).Returns(false);
        tagConfiguration.Setup(t => t.Priorities).Returns([new MessagePriorityOption { Name = "Normal", Value = 0 }]);
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1", tag: "URGENT") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, tagConfiguration.Object);

        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        Assert.Null(vm.Entries[0].TagText);
    }

    /// <summary>DeleteEntry passes the entry's IsOutboundMessage flag through to the service so self-addressed duplicates are disambiguated.</summary>
    [Fact]
    public async Task DeleteEntry_OutboundMessage_PassesFlagToService()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1") }, Total: 1));
        svc.Setup(s => s.DeleteEntry(It.IsAny<string>(), It.IsAny<EntryType>(), It.IsAny<bool>())).Returns(Task.CompletedTask);
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-outbox", FolderType.Outbox));
        EntryItemViewModel entry = vm.Entries[0];

        await vm.DeleteEntry(entry);

        svc.Verify(s => s.DeleteEntry("M1", EntryType.Message, true), Times.Once);
    }

    /// <summary>UpdateEntryStatus sets OverallStatus on the matching message entry.</summary>
    [Fact]
    public async Task UpdateEntryStatus_SetsStatusOnMatchingEntry()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("MSG42") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, format);
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
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-outbox", FolderType.Outbox));

        Exception? ex = await Record.ExceptionAsync(() => vm.UpdateEntryStatus("UNKNOWN", DestinationStatus.Failed));
        Assert.Null(ex);
    }

    /// <summary>DeleteEntry calls the service and removes the item from Entries.</summary>
    [Fact]
    public async Task DeleteEntry_CallsServiceAndRemovesFromList()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1") }, Total: 1));
        svc.Setup(s => s.DeleteEntry(It.IsAny<string>(), It.IsAny<EntryType>())).Returns(Task.CompletedTask);
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        EntryItemViewModel entry = vm.Entries[0];

        await vm.DeleteEntry(entry);

        Assert.Empty(vm.Entries);
        svc.Verify(s => s.DeleteEntry("M1", EntryType.Message), Times.Once);
    }

    /// <summary>DeleteEntry is a silent no-op when IEngineController.CanDelete forbids deletion for the active folder's root type.</summary>
    [Fact]
    public async Task DeleteEntry_ForbiddenByController_DoesNotCallServiceOrRemoveFromList()
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.CanDelete(FolderType.Inbox)).Returns(false);

        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, controller.Object);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        EntryItemViewModel entry = vm.Entries[0];

        await vm.DeleteEntry(entry);

        Assert.Single(vm.Entries);
        svc.Verify(s => s.DeleteEntry(It.IsAny<string>(), It.IsAny<EntryType>(), It.IsAny<bool>()), Times.Never);
    }

    /// <summary>SetPendingSelectId causes the matching entry to be auto-selected after the next refresh.</summary>
    [Fact]
    public async Task SetPendingSelectId_AutoSelectsAfterRefresh()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetDrafts(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
           .ReturnsAsync((Items: new List<DraftEntity> { MakeDraft() }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-drafts", FolderType.Drafts));
        string id = vm.Entries[0].Id;

        vm.SetPendingSelectId(id);
        await vm.Refresh();

        Assert.NotNull(vm.SelectedEntry);
        Assert.Equal(id, vm.SelectedEntry.Id);
    }
}
