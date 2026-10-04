namespace BlueHeighliner.Comlink.Tests.Unit.Internal.ViewModels;

/// <summary>Unit tests for <see cref="EntryBarViewModel"/>.</summary>
public sealed class EntryBarViewModelTests
{
    private static readonly IEngineController format = new TestEngineController();

    private static FolderItemViewModel MakeFolder(string id, FolderType type)
        => new(id, type.ToString(), type, null);

    private static MessageEntity MakeMessage(string id = "MSG1", string fromUser = "ALPHA", string body = "Hello", int priority = 0, string tag = "", string securityLevel = "", bool isAlert = false)
    {
        object message = format.CreateFrame();
        ((TestFrame)message).MessageId = id;
        format.SetFromUser(message, fromUser);
        ((TestFrame)message).Body = body;
        ((TestFrame)message).Priority = priority switch { 0 => "NORMAL", 1 => "Medium", 2 => "High", _ => $"LEVEL{priority}" };
        ((TestFrame)message).Tag = tag;
        ((TestFrame)message).SecurityLevel = securityLevel;
        ((TestFrame)message).IsAlert = isAlert;
        return new MessageEntity
        {
            MessageId = id,
            Message = message,
            ReceivedAt = DateTime.UtcNow,
            DeliveryStatuses = []
        };
    }

    private static DraftEntity MakeDraft(string body = "Draft body", bool isAlert = false)
        => new()
        {
            Id = new ObjectId(),
            Body = body,
            FolderId = "root-drafts",
            ModifiedAt = DateTime.UtcNow,
            Addresses = [],
            IsAlert = isAlert
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

    /// <summary>ShowSearch is true for Inbox, Outbox, Drafts and Notes, and false for Activity, which has no free-text fields worth searching.</summary>
    [Theory]
    [InlineData(FolderType.Inbox, true)]
    [InlineData(FolderType.Outbox, true)]
    [InlineData(FolderType.Drafts, true)]
    [InlineData(FolderType.Notes, true)]
    [InlineData(FolderType.Activity, false)]
    public async Task LoadFolder_SetsShowSearchByFolderType(FolderType folderType, bool expected)
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>()))
           .ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        svc.Setup(s => s.GetDrafts(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<EntryFilter>()))
           .ReturnsAsync((Items: new List<DraftEntity>(), Total: 0));
        svc.Setup(s => s.GetNotes(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<EntryFilter>()))
           .ReturnsAsync((Items: new List<NoteEntity>(), Total: 0));
        svc.Setup(s => s.GetActivityLogs(It.IsAny<int>()))
           .ReturnsAsync((Items: new List<ActivityLogEntity>(), Total: 0));
        EntryBarViewModel vm = new(svc.Object, format);

        await vm.LoadFolder(MakeFolder("root", folderType));

        Assert.Equal(expected, vm.ShowSearch);
    }

    /// <summary>LoadFolder resets any search text left over from a previously viewed folder.</summary>
    [Fact]
    public async Task LoadFolder_ResetsSearchTextFromPreviousFolder()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>()))
           .ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        vm.SearchText = "leftover";

        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        Assert.Equal(string.Empty, vm.SearchText);
    }

    /// <summary>Setting SearchText resets to the first page and reloads with the search term passed through to the entry service.</summary>
    [Fact]
    public async Task SearchText_Set_ResetsPageAndPassesSearchToService()
    {
        Mock<IEntryService> svc = new();
        MessageEntity match = MakeMessage(body: "Quarterly Report");
        svc.SetupSequence(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage() }, Total: 1))
           .ReturnsAsync((Items: new List<MessageEntity> { match }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        vm.CurrentPage = 2;

        vm.SearchText = "report";

        Assert.Equal(1, vm.CurrentPage);
        svc.Verify(s => s.GetMessages("root-inbox", 1, new EntryFilter { Search = "report" }), Times.Once);
    }

    /// <summary>An empty or whitespace-only SearchText is passed to the entry service as null, matching the unfiltered default rather than an empty search.</summary>
    [Fact]
    public async Task SearchText_Whitespace_PassesNullToService()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>()))
           .ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        vm.SearchText = "   ";

        svc.Verify(s => s.GetMessages("root-inbox", 1, null), Times.AtLeastOnce);
    }

    /// <summary>AvailableSecurityLevelFilters is a leading "Any" option followed by every configured security level.</summary>
    [Fact]
    public void AvailableSecurityLevelFilters_IsAnyFollowedByEveryConfiguredLevel()
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.SecurityLevels).Returns([new SecurityLevel { Name = "PUBLIC", Color = "#2E7D32" }, new SecurityLevel { Name = "RESTRICTED", Color = "#C62828" }]);
        EntryBarViewModel vm = new(new Mock<IEntryService>().Object, controller.Object);

        Assert.Equal(["Any", "PUBLIC", "RESTRICTED"], vm.AvailableSecurityLevelFilters.Select(f => f.Label));
        Assert.Equal([null, "PUBLIC", "RESTRICTED"], vm.AvailableSecurityLevelFilters.Select(f => f.Name));
        Assert.Same(vm.AvailableSecurityLevelFilters[0], vm.SelectedSecurityLevelFilter);
    }

    /// <summary>AvailablePriorityFilters is a leading "Any" option followed by every configured priority.</summary>
    [Fact]
    public void AvailablePriorityFilters_IsAnyFollowedByEveryConfiguredPriority()
    {
        EntryBarViewModel vm = new(new Mock<IEntryService>().Object, new EngineController(EngineBuilder.Build(new TestEngineConfiguration()), new CurrentUserProvider(), null));

        Assert.Equal(["Any", .. Enum.GetValues<TestMessagePriority>().Select(p => p.ToString().ToUpperInvariant())], vm.AvailablePriorityFilters.Select(f => f.Label));
        Assert.Equal([(Enum?)null, .. Enum.GetValues<TestMessagePriority>().Cast<Enum>()], vm.AvailablePriorityFilters.Select(f => f.Value));
        Assert.Same(vm.AvailablePriorityFilters[0], vm.SelectedPriorityFilter);
    }

    /// <summary>ShowSecurityLevelFilter is true only for Inbox, Outbox and Drafts, and only when at least one security level is configured.</summary>
    [Theory]
    [InlineData(FolderType.Inbox, true)]
    [InlineData(FolderType.Outbox, true)]
    [InlineData(FolderType.Drafts, true)]
    [InlineData(FolderType.Notes, false)]
    [InlineData(FolderType.Activity, false)]
    public async Task LoadFolder_SetsShowSecurityLevelFilterWhenLevelsConfigured(FolderType folderType, bool expected)
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.SecurityLevels).Returns([new SecurityLevel { Name = "PUBLIC", Color = "#2E7D32" }]);
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>())).ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        svc.Setup(s => s.GetDrafts(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<EntryFilter>())).ReturnsAsync((Items: new List<DraftEntity>(), Total: 0));
        svc.Setup(s => s.GetNotes(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<EntryFilter>())).ReturnsAsync((Items: new List<NoteEntity>(), Total: 0));
        svc.Setup(s => s.GetActivityLogs(It.IsAny<int>())).ReturnsAsync((Items: new List<ActivityLogEntity>(), Total: 0));
        EntryBarViewModel vm = new(svc.Object, controller.Object);

        await vm.LoadFolder(MakeFolder("root", folderType));

        Assert.Equal(expected, vm.ShowSecurityLevelFilter);
    }

    /// <summary>ShowSecurityLevelFilter is false for a message folder when no security levels are configured, even though ShowPriorityFilter and ShowAlertFilter still apply.</summary>
    [Fact]
    public async Task LoadFolder_NoSecurityLevelsConfigured_HidesSecurityLevelFilterOnly()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>())).ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.SecurityLevels).Returns([]);
        EntryBarViewModel vm = new(svc.Object, controller.Object);

        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        Assert.False(vm.ShowSecurityLevelFilter);
        Assert.True(vm.ShowPriorityFilter);
        Assert.True(vm.ShowAlertFilter);
    }

    /// <summary>Setting any of the explicit filter controls resets to the first page and passes the built filter through to the entry service.</summary>
    [Fact]
    public async Task SelectedPriorityFilter_Set_ResetsPageAndPassesFilterToService()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>())).ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        vm.CurrentPage = 2;

        vm.SelectedPriorityFilter = vm.AvailablePriorityFilters.Single(f => f.Label == "NORMAL");

        Assert.Equal(1, vm.CurrentPage);
        svc.Verify(s => s.GetMessages("root-inbox", 1, new EntryFilter { Priority = TestMessagePriority.Normal }), Times.Once);
    }

    /// <summary>Setting AlertOnlyFilter passes an EntryFilter with AlertOnly true through to the entry service.</summary>
    [Fact]
    public async Task AlertOnlyFilter_SetTrue_PassesAlertOnlyFilterToService()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>())).ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        vm.AlertOnlyFilter = true;

        svc.Verify(s => s.GetMessages("root-inbox", 1, new EntryFilter { AlertOnly = true }), Times.Once);
    }

    /// <summary>The author filter is shown for the Inbox only and passes the trimmed text through as EntryFilter.Author.</summary>
    [Fact]
    public async Task AuthorFilter_Inbox_PassesAuthorToServiceAndCountsAsActive()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>())).ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        vm.AuthorFilter = "  alice ";

        Assert.True(vm.ShowAuthorFilter);
        Assert.False(vm.ShowDestinationFilter);
        Assert.Equal(1, vm.ActiveFilterCount);
        svc.Verify(s => s.GetMessages("root-inbox", 1, new EntryFilter { Author = "alice" }), Times.Once);
    }

    /// <summary>The destination filter is shown for the Outbox and Drafts only and passes through as EntryFilter.Destination.</summary>
    [Fact]
    public async Task DestinationFilter_Outbox_PassesDestinationToService()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>())).ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-outbox", FolderType.Outbox));

        vm.DestinationFilter = "bob";

        Assert.True(vm.ShowDestinationFilter);
        Assert.False(vm.ShowAuthorFilter);
        svc.Verify(s => s.GetMessages("root-outbox", 1, new EntryFilter { Destination = "bob" }), Times.Once);
    }

    /// <summary>Loading another folder clears the author and destination text.</summary>
    [Fact]
    public async Task LoadFolder_ClearsAuthorAndDestinationFilters()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>())).ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        vm.AuthorFilter = "alice";

        await vm.LoadFolder(MakeFolder("root-outbox", FolderType.Outbox));

        Assert.Equal(string.Empty, vm.AuthorFilter);
        Assert.Equal(string.Empty, vm.DestinationFilter);
        Assert.Equal(0, vm.ActiveFilterCount);
    }

    /// <summary>LoadFolder resets every filter control left over from a previously viewed folder, not just the search text.</summary>
    [Fact]
    public async Task LoadFolder_ResetsAllFiltersFromPreviousFolder()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>())).ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        vm.SelectedPriorityFilter = vm.AvailablePriorityFilters.Single(f => f.Label == "NORMAL");
        vm.AlertOnlyFilter = true;
        vm.DateFrom = DateTimeOffset.Now;
        vm.DateTo = DateTimeOffset.Now;

        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        Assert.Same(vm.AvailablePriorityFilters[0], vm.SelectedPriorityFilter);
        Assert.False(vm.AlertOnlyFilter);
        Assert.Null(vm.DateFrom);
        Assert.Null(vm.DateTo);
    }

    /// <summary>The filter section is collapsed by default.</summary>
    [Fact]
    public void IsFiltersExpanded_DefaultsToFalse()
    {
        EntryBarViewModel vm = new(new Mock<IEntryService>().Object, format);

        Assert.False(vm.IsFiltersExpanded);
        Assert.Equal("▼", vm.FiltersExpandIndicator);
    }

    /// <summary>ToggleFiltersCommand flips IsFiltersExpanded and updates the expand indicator.</summary>
    [Fact]
    public void ToggleFiltersCommand_FlipsIsFiltersExpanded()
    {
        EntryBarViewModel vm = new(new Mock<IEntryService>().Object, format);

        vm.ToggleFiltersCommand.Execute(null);
        Assert.True(vm.IsFiltersExpanded);
        Assert.Equal("▲", vm.FiltersExpandIndicator);

        vm.ToggleFiltersCommand.Execute(null);
        Assert.False(vm.IsFiltersExpanded);
        Assert.Equal("▼", vm.FiltersExpandIndicator);
    }

    /// <summary>Collapsing the filter section does not clear or disable any filter already set - only its visibility changes.</summary>
    [Fact]
    public async Task ToggleFiltersCommand_CollapsingDoesNotClearActiveFilters()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>())).ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        vm.ToggleFiltersCommand.Execute(null);
        vm.AlertOnlyFilter = true;

        vm.ToggleFiltersCommand.Execute(null);

        Assert.False(vm.IsFiltersExpanded);
        Assert.True(vm.AlertOnlyFilter);
        svc.Verify(s => s.GetMessages("root-inbox", 1, new EntryFilter { AlertOnly = true }), Times.AtLeastOnce);
    }

    /// <summary>ActiveFilterCount and HasActiveFilters reflect exactly the filter section's own criteria, not SearchText.</summary>
    [Fact]
    public async Task ActiveFilterCount_ReflectsOnlySetFilterCriteria()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>())).ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        Assert.Equal(0, vm.ActiveFilterCount);
        Assert.False(vm.HasActiveFilters);

        vm.SearchText = "ignored for this count";
        Assert.Equal(0, vm.ActiveFilterCount);

        vm.AlertOnlyFilter = true;
        vm.DateFrom = DateTimeOffset.Now;

        Assert.Equal(2, vm.ActiveFilterCount);
        Assert.True(vm.HasActiveFilters);
    }

    /// <summary>Search combines with an active filter criterion: a matching entry must satisfy both, not either - the filter panel narrows exactly what search can return, it is never bypassed by search.</summary>
    [Fact]
    public async Task SearchAndFilter_CombineWithAnd_NeitherBypassesTheOther()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>())).ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        vm.AlertOnlyFilter = true;

        vm.SearchText = "report";

        svc.Verify(s => s.GetMessages("root-inbox", 1, new EntryFilter { Search = "report", AlertOnly = true }), Times.Once);
    }

    /// <summary>Inbox, Outbox and Draft entries carry IsAlert through from the stored message/draft, driving the entry's title color.</summary>
    [Theory]
    [InlineData(FolderType.Inbox)]
    [InlineData(FolderType.Outbox)]
    public async Task LoadFolder_MessageFolder_SetsIsAlertOnEntry(FolderType folderType)
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage(isAlert: true) }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, format);

        await vm.LoadFolder(MakeFolder("root", folderType));

        Assert.True(Assert.Single(vm.Entries).IsAlert);
    }

    /// <summary>Draft entries carry IsAlert through from DraftEntity.IsAlert.</summary>
    [Fact]
    public async Task LoadFolder_Drafts_SetsIsAlertOnEntry()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetDrafts(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<EntryFilter>()))
           .ReturnsAsync((Items: new List<DraftEntity> { MakeDraft(isAlert: true) }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, format);

        await vm.LoadFolder(MakeFolder("root-drafts", FolderType.Drafts));

        Assert.True(Assert.Single(vm.Entries).IsAlert);
    }

    /// <summary>Picking only a DateFrom/DateTo date, with no time, still covers the whole day - midnight through the day's last instant.</summary>
    [Fact]
    public async Task DateFilters_DateOnlyNoTime_CoverWholeDay()
    {
        DateTimeOffset day = new(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>())).ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        vm.DateFrom = day;
        vm.DateTo = day;

        svc.Verify(s => s.GetMessages("root-inbox", 1, new EntryFilter
        {
            DateFrom = day.Date,
            DateTo = day.Date + new TimeSpan(0, 23, 59, 59, 999)
        }), Times.Once);
    }

    /// <summary>An explicit TimeFrom/TimeTo narrows the date bounds to that exact time of day instead of the whole day.</summary>
    [Fact]
    public async Task DateFilters_WithExplicitTime_NarrowsToThatInstant()
    {
        DateTimeOffset day = new(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>())).ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        vm.DateFrom = day;
        vm.DateTo = day;

        vm.TimeFrom = new TimeSpan(9, 0, 0);
        vm.TimeTo = new TimeSpan(17, 0, 0);

        svc.Verify(s => s.GetMessages("root-inbox", 1, new EntryFilter
        {
            DateFrom = day.Date + new TimeSpan(9, 0, 0),
            DateTo = day.Date + new TimeSpan(17, 0, 0)
        }), Times.Once);
    }

    /// <summary>ResetFiltersCommand clears every filter section criterion back to its default, resets to the first page, and reloads unfiltered - SearchText is untouched.</summary>
    [Fact]
    public async Task ResetFiltersCommand_ClearsEveryFilterCriterionButNotSearch()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<EntryFilter>())).ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        vm.SearchText = "keep me";
        vm.DateFrom = DateTimeOffset.Now;
        vm.TimeFrom = new TimeSpan(9, 0, 0);
        vm.DateTo = DateTimeOffset.Now;
        vm.TimeTo = new TimeSpan(17, 0, 0);
        vm.AlertOnlyFilter = true;
        vm.CurrentPage = 2;

        vm.ResetFiltersCommand.Execute(null);

        Assert.Equal("keep me", vm.SearchText);
        Assert.Null(vm.DateFrom);
        Assert.Null(vm.TimeFrom);
        Assert.Null(vm.DateTo);
        Assert.Null(vm.TimeTo);
        Assert.False(vm.AlertOnlyFilter);
        Assert.Same(vm.AvailableSecurityLevelFilters[0], vm.SelectedSecurityLevelFilter);
        Assert.Same(vm.AvailablePriorityFilters[0], vm.SelectedPriorityFilter);
        Assert.Equal(1, vm.CurrentPage);
        Assert.Equal(0, vm.ActiveFilterCount);
        svc.Verify(s => s.GetMessages("root-inbox", 1, new EntryFilter { Search = "keep me" }), Times.AtLeastOnce);
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
            new MessagePriorityOption { Name = "Low", Value = 0, Key = TestMessagePriority.Low },
            new MessagePriorityOption { Name = "Medium", Value = 1, Key = TestMessagePriority.Medium },
            new MessagePriorityOption { Name = "High", Value = 2, Key = TestMessagePriority.High }
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
            new MessagePriorityOption { Name = "Low", Value = 0, Key = TestMessagePriority.Low },
            new MessagePriorityOption { Name = "Medium", Value = 1, Key = TestMessagePriority.Medium },
            new MessagePriorityOption { Name = "High", Value = 2, Key = TestMessagePriority.High }
        ]);
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1", priority: 1) }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, priorityProvider.Object);

        await vm.LoadFolder(MakeFolder("root-outbox", FolderType.Outbox));

        Assert.Equal("Medium", vm.Entries[0].PriorityText);
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
        tagConfiguration.Setup(t => t.Priorities).Returns([new MessagePriorityOption { Name = "Normal", Value = 0, Key = TestMessagePriority.Normal }]);
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1", tag: "URGENT") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, tagConfiguration.Object);

        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        Assert.Null(vm.Entries[0].TagText);
    }

    /// <summary>Inbox entries carry a SecurityLevelColorHex resolved from the message's stored security level.</summary>
    [Fact]
    public async Task LoadFolder_Inbox_RecognizedSecurityLevel_SetsSecurityLevelColorHex()
    {
        Mock<TestEngineController> securityLevelProvider = new() { CallBase = true };
        securityLevelProvider.Setup(p => p.SecurityLevels).Returns([new SecurityLevel { Name = "RESTRICTED", Color = "#C62828" }]);
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1", securityLevel: "RESTRICTED") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, securityLevelProvider.Object);

        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        Assert.Equal("#C62828", vm.Entries[0].SecurityLevelColorHex);
    }

    /// <summary>Outbox entries carry a SecurityLevelColorHex the same way as Inbox entries.</summary>
    [Fact]
    public async Task LoadFolder_Outbox_RecognizedSecurityLevel_SetsSecurityLevelColorHex()
    {
        Mock<TestEngineController> securityLevelProvider = new() { CallBase = true };
        securityLevelProvider.Setup(p => p.SecurityLevels).Returns([new SecurityLevel { Name = "RESTRICTED", Color = "#C62828" }]);
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1", securityLevel: "RESTRICTED") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, securityLevelProvider.Object);

        await vm.LoadFolder(MakeFolder("root-outbox", FolderType.Outbox));

        Assert.Equal("#C62828", vm.Entries[0].SecurityLevelColorHex);
    }

    /// <summary>An unrecognized or empty stored security level yields a null SecurityLevelColorHex rather than a fallback color.</summary>
    [Fact]
    public async Task LoadFolder_Inbox_NoSecurityLevel_SecurityLevelColorHexIsNull()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, format);

        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        Assert.Null(vm.Entries[0].SecurityLevelColorHex);
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

        await vm.UpdateEntryStatus("MSG42", DestinationStatus.Received);

        Assert.Equal(DestinationStatus.Received, vm.Entries[0].OverallStatus);
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

    /// <summary>DeleteEntry raises EntryDeleted with the deleted entry after removing it.</summary>
    [Fact]
    public async Task DeleteEntry_RaisesEntryDeleted()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1") }, Total: 1));
        svc.Setup(s => s.DeleteEntry(It.IsAny<string>(), It.IsAny<EntryType>())).Returns(Task.CompletedTask);
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        EntryItemViewModel entry = vm.Entries[0];
        List<EntryItemViewModel> raised = [];
        vm.EntryDeleted += raised.Add;

        await vm.DeleteEntry(entry);

        Assert.Equal([entry], raised);
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
        bool raised = false;
        vm.EntryDeleted += _ => raised = true;

        await vm.DeleteEntry(entry);

        Assert.Single(vm.Entries);
        svc.Verify(s => s.DeleteEntry(It.IsAny<string>(), It.IsAny<EntryType>(), It.IsAny<bool>()), Times.Never);
        Assert.False(raised);
    }

    /// <summary>LoadFolder sets CanDeleteEntries from IEngineController.CanDelete for the folder's root type.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoadFolder_SetsCanDeleteEntriesFromController(bool canDelete)
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.CanDelete(FolderType.Inbox)).Returns(canDelete);
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity>(), Total: 0));
        EntryBarViewModel vm = new(svc.Object, controller.Object);

        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));

        Assert.Equal(canDelete, vm.CanDeleteEntries);
    }

    /// <summary>The generated DeleteCommand, bound to the entry list's right-click "Delete" menu item, delegates to DeleteEntry.</summary>
    [Fact]
    public async Task DeleteCommand_CallsServiceAndRemovesFromList()
    {
        Mock<IEntryService> svc = new();
        svc.Setup(s => s.GetMessages(It.IsAny<string>(), It.IsAny<int>()))
           .ReturnsAsync((Items: new List<MessageEntity> { MakeMessage("M1") }, Total: 1));
        svc.Setup(s => s.DeleteEntry(It.IsAny<string>(), It.IsAny<EntryType>())).Returns(Task.CompletedTask);
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-inbox", FolderType.Inbox));
        EntryItemViewModel entry = vm.Entries[0];

        await vm.DeleteCommand.ExecuteAsync(entry);

        Assert.Empty(vm.Entries);
        svc.Verify(s => s.DeleteEntry("M1", EntryType.Message), Times.Once);
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

    /// <summary>Two refreshes that overlap list each entry once: only the newer load is shown, rather than both loads appending.</summary>
    [Fact]
    public async Task Refresh_Overlapping_ShowsNewestLoadOnly()
    {
        Mock<IEntryService> svc = new();
        TaskCompletionSource<(List<NoteEntity> Items, int Total)> first = new();
        svc.SetupSequence(s => s.GetNotes(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
           .Returns(first.Task)
           .ReturnsAsync((Items: new List<NoteEntity> { MakeNote("Newer") }, Total: 1));
        EntryBarViewModel vm = new(svc.Object, format);

        Task slow = vm.LoadFolder(MakeFolder("root-notes", FolderType.Notes));
        await vm.Refresh();
        first.SetResult((Items: [MakeNote("Older")], Total: 1));
        await slow;

        Assert.Equal("Newer", Assert.Single(vm.Entries).Title);
    }

    /// <summary>Deleting from the list updates the page count, not just the list.</summary>
    [Fact]
    public async Task DeleteEntry_UpdatesPagination()
    {
        Mock<IEntryService> svc = new();
        NoteEntity note = MakeNote("Only");
        svc.SetupSequence(s => s.GetNotes(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
           .ReturnsAsync((Items: new List<NoteEntity> { note }, Total: 51))
           .ReturnsAsync((Items: new List<NoteEntity>(), Total: 50));
        EntryBarViewModel vm = new(svc.Object, format);
        await vm.LoadFolder(MakeFolder("root-notes", FolderType.Notes));
        Assert.Equal(2, vm.TotalPages);

        await vm.DeleteEntry(vm.Entries[0]);

        Assert.Equal(1, vm.TotalPages);
    }
}
