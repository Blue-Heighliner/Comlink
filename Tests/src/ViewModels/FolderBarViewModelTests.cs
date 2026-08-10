namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="FolderBarViewModel"/>.</summary>
public sealed class FolderBarViewModelTests
{
    private static Folder MakeFolder(string id, FolderType type, IReadOnlyList<Folder>? children = null)
        => new()
        {
            Id = id,
            Name = type.ToString(),
            RootType = type,
            ParentId = null,
            Children = children ?? []
        };

    private static (FolderBarViewModel Vm, Mock<IFolderRepository> FoldersMock, Mock<IEntryService> ServiceMock) Build(
        List<Folder>? tree = null)
    {
        Mock<IFolderRepository> foldersMock = new();
        Mock<IEntryService> serviceMock = new();
        foldersMock.Setup(f => f.GetTree()).ReturnsAsync(tree ?? []);
        return (new FolderBarViewModel(foldersMock.Object, serviceMock.Object), foldersMock, serviceMock);
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    /// <summary>Load populates RootFolders in the canonical Inbox/Outbox/Drafts/Notes/Activity order.</summary>
    [Fact]
    public async Task Load_PopulatesRootFoldersInOrder()
    {
        List<Folder> tree =
        [
            MakeFolder("activity", FolderType.Activity),
            MakeFolder("drafts",   FolderType.Drafts),
            MakeFolder("notes",    FolderType.Notes),
            MakeFolder("outbox",   FolderType.Outbox),
            MakeFolder("inbox",    FolderType.Inbox),
        ];
        (FolderBarViewModel vm, _, _) = Build(tree);

        await vm.Load();

        Assert.Equal(5, vm.RootFolders.Count);
        Assert.Equal(FolderType.Inbox,    vm.RootFolders[0].RootType);
        Assert.Equal(FolderType.Outbox,   vm.RootFolders[1].RootType);
        Assert.Equal(FolderType.Drafts,   vm.RootFolders[2].RootType);
        Assert.Equal(FolderType.Notes,    vm.RootFolders[3].RootType);
        Assert.Equal(FolderType.Activity, vm.RootFolders[4].RootType);
    }

    /// <summary>Load selects the first folder (Inbox) after populating.</summary>
    [Fact]
    public async Task Load_SelectsFirstFolder()
    {
        List<Folder> tree =
        [
            MakeFolder("inbox",  FolderType.Inbox),
            MakeFolder("outbox", FolderType.Outbox)
        ];
        (FolderBarViewModel vm, _, _) = Build(tree);

        await vm.Load();

        Assert.NotNull(vm.SelectedFolder);
        Assert.Equal(FolderType.Inbox, vm.SelectedFolder.RootType);
    }

    /// <summary>Children are recursively built from nested folders.</summary>
    [Fact]
    public async Task Load_BuildsChildrenRecursively()
    {
        Folder child = new()
        {
            Id = "child1",
            Name = "Sub",
            RootType = FolderType.Inbox,
            ParentId = "inbox"
        };
        Folder inbox = new()
        {
            Id = "inbox",
            Name = "Inbox",
            RootType = FolderType.Inbox,
            ParentId = null,
            Children = [child]
        };
        (FolderBarViewModel vm, _, _) = Build([inbox]);

        await vm.Load();

        Assert.Single(vm.RootFolders[0].Children);
        Assert.Equal("child1", vm.RootFolders[0].Children[0].Id);
    }

    // ── SelectFolder ──────────────────────────────────────────────────────────

    /// <summary>SelectFolder fires FolderSelected event and updates SelectedFolder.</summary>
    [Fact]
    public async Task SelectFolder_FiresEventAndUpdatesSelection()
    {
        List<Folder> tree = [MakeFolder("inbox", FolderType.Inbox), MakeFolder("outbox", FolderType.Outbox)];
        (FolderBarViewModel vm, _, _) = Build(tree);
        await vm.Load();
        FolderItemViewModel? received = null;
        vm.FolderSelected += f => received = f;

        vm.SelectFolder(vm.RootFolders[1]);

        Assert.Same(vm.RootFolders[1], vm.SelectedFolder);
        Assert.Same(vm.RootFolders[1], received);
    }

    /// <summary>SelectFolder deselects the previously selected folder.</summary>
    [Fact]
    public async Task SelectFolder_DeselectedPrevious()
    {
        List<Folder> tree = [MakeFolder("inbox", FolderType.Inbox), MakeFolder("outbox", FolderType.Outbox)];
        (FolderBarViewModel vm, _, _) = Build(tree);
        await vm.Load();
        FolderItemViewModel first = vm.RootFolders[0];

        vm.SelectFolder(vm.RootFolders[1]);

        Assert.False(first.IsSelected);
    }

    // ── SelectFolderByType ────────────────────────────────────────────────────

    /// <summary>SelectFolderByType selects the matching root folder.</summary>
    [Fact]
    public async Task SelectFolderByType_SelectsMatchingFolder()
    {
        List<Folder> tree = [MakeFolder("inbox", FolderType.Inbox), MakeFolder("drafts", FolderType.Drafts)];
        (FolderBarViewModel vm, _, _) = Build(tree);
        await vm.Load();

        vm.SelectFolderByType(FolderType.Drafts);

        Assert.Equal(FolderType.Drafts, vm.SelectedFolder?.RootType);
    }

    // ── MoveEntry ─────────────────────────────────────────────────────────────

    /// <summary>MoveEntry calls entryService.MoveEntry for a compatible type combination.</summary>
    [Fact]
    public async Task MoveEntry_CompatibleTypes_CallsMoveEntry()
    {
        List<Folder> tree = [MakeFolder("inbox", FolderType.Inbox)];
        (FolderBarViewModel vm, _, Mock<IEntryService> svc) = Build(tree);
        svc.Setup(s => s.MoveEntry(It.IsAny<string>(), It.IsAny<EntryType>(), It.IsAny<string>()))
           .Returns(Task.CompletedTask);
        await vm.Load();
        EntryItemViewModel entry = new("msg1", "Title", EntryType.Message, DateTime.UtcNow);
        FolderItemViewModel inbox = vm.RootFolders[0];

        await vm.MoveEntry(entry, inbox);

        svc.Verify(s => s.MoveEntry("msg1", EntryType.Message, "inbox"), Times.Once);
    }

    /// <summary>MoveEntry passes the entry's IsOutboundMessage flag through so self-addressed duplicates are disambiguated.</summary>
    [Fact]
    public async Task MoveEntry_OutboundMessage_PassesFlagToService()
    {
        List<Folder> tree = [MakeFolder("outbox", FolderType.Outbox)];
        (FolderBarViewModel vm, _, Mock<IEntryService> svc) = Build(tree);
        svc.Setup(s => s.MoveEntry(It.IsAny<string>(), It.IsAny<EntryType>(), It.IsAny<string>(), It.IsAny<bool>()))
           .Returns(Task.CompletedTask);
        await vm.Load();
        EntryItemViewModel entry = new("msg1", "Title", EntryType.Message, DateTime.UtcNow, isOutboundMessage: true);
        FolderItemViewModel outbox = vm.RootFolders[0];

        await vm.MoveEntry(entry, outbox);

        svc.Verify(s => s.MoveEntry("msg1", EntryType.Message, "outbox", true), Times.Once);
    }

    /// <summary>MoveEntry does nothing for an incompatible type combination.</summary>
    [Fact]
    public async Task MoveEntry_IncompatibleTypes_DoesNothing()
    {
        List<Folder> tree = [MakeFolder("drafts", FolderType.Drafts)];
        (FolderBarViewModel vm, _, Mock<IEntryService> svc) = Build(tree);
        await vm.Load();
        EntryItemViewModel msgEntry = new("msg1", "Title", EntryType.Message, DateTime.UtcNow);

        await vm.MoveEntry(msgEntry, vm.RootFolders[0]);

        svc.Verify(s => s.MoveEntry(It.IsAny<string>(), It.IsAny<EntryType>(), It.IsAny<string>()), Times.Never);
    }

    // ── IsCompatibleMove ──────────────────────────────────────────────────────

    /// <summary>Messages may only be moved into Inbox or Outbox.</summary>
    [Theory]
    [InlineData(FolderType.Inbox,    true)]
    [InlineData(FolderType.Outbox,   true)]
    [InlineData(FolderType.Drafts,   false)]
    [InlineData(FolderType.Notes,    false)]
    [InlineData(FolderType.Activity, false)]
    public void IsCompatibleMove_MessageCompatibility(FolderType folderType, bool expected)
    {
        Assert.Equal(expected, FolderBarViewModel.IsCompatibleMove(EntryType.Message, folderType));
    }

    /// <summary>Drafts may only be moved into the Drafts folder.</summary>
    [Theory]
    [InlineData(FolderType.Drafts, true)]
    [InlineData(FolderType.Inbox,  false)]
    public void IsCompatibleMove_DraftCompatibility(FolderType folderType, bool expected)
    {
        Assert.Equal(expected, FolderBarViewModel.IsCompatibleMove(EntryType.Draft, folderType));
    }

    /// <summary>Notes may only be moved into the Notes folder.</summary>
    [Fact]
    public void IsCompatibleMove_NoteCompatibility()
    {
        Assert.True(FolderBarViewModel.IsCompatibleMove(EntryType.Note, FolderType.Notes));
        Assert.False(FolderBarViewModel.IsCompatibleMove(EntryType.Note, FolderType.Inbox));
    }

    // ── CollapseAll ───────────────────────────────────────────────────────────

    /// <summary>CollapseAll sets IsExpanded = false on all folders in the tree.</summary>
    [Fact]
    public async Task CollapseAll_CollapsesAllFolders()
    {
        Folder child = new() { Id = "c", Name = "Sub", RootType = FolderType.Inbox, ParentId = "inbox" };
        Folder inbox = new() { Id = "inbox", Name = "Inbox", RootType = FolderType.Inbox, Children = [child] };
        (FolderBarViewModel vm, _, _) = Build([inbox]);
        await vm.Load();
        vm.RootFolders[0].IsExpanded = true;
        vm.RootFolders[0].Children[0].IsExpanded = true;

        vm.CollapseAll();

        Assert.False(vm.RootFolders[0].IsExpanded);
        Assert.False(vm.RootFolders[0].Children[0].IsExpanded);
    }
}
