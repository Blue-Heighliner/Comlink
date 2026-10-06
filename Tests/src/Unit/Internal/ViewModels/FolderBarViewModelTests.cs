namespace BlueHeighliner.Comlink.Tests.Unit.Internal.ViewModels;

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
        List<Folder>? tree = null, bool canDelete = true, bool separateAlerts = false)
    {
        Mock<IFolderRepository> foldersMock = new();
        Mock<IEntryService> serviceMock = new();
        Mock<IEngineController> controllerMock = new();
        controllerMock.Setup(c => c.CanDelete(It.IsAny<FolderType>())).Returns(canDelete);
        controllerMock.Setup(c => c.SeparateAlerts).Returns(separateAlerts);
        foldersMock.Setup(f => f.GetTree()).ReturnsAsync(tree ?? []);
        return (new FolderBarViewModel(foldersMock.Object, serviceMock.Object, controllerMock.Object), foldersMock, serviceMock);
    }

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
        Assert.Equal(FolderType.Inbox, vm.RootFolders[0].RootType);
        Assert.Equal(FolderType.Outbox, vm.RootFolders[1].RootType);
        Assert.Equal(FolderType.Drafts, vm.RootFolders[2].RootType);
        Assert.Equal(FolderType.Notes, vm.RootFolders[3].RootType);
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

    /// <summary>DeselectFolder clears SelectedFolder and unmarks the folder's IsSelected flag.</summary>
    [Fact]
    public async Task DeselectFolder_ClearsSelectionAndFlag()
    {
        List<Folder> tree = [MakeFolder("inbox", FolderType.Inbox)];
        (FolderBarViewModel vm, _, _) = Build(tree);
        await vm.Load();
        FolderItemViewModel selected = vm.RootFolders[0];

        vm.DeselectFolder();

        Assert.Null(vm.SelectedFolder);
        Assert.False(selected.IsSelected);
    }

    /// <summary>DeselectFolder does not raise FolderSelected.</summary>
    [Fact]
    public async Task DeselectFolder_DoesNotRaiseFolderSelected()
    {
        List<Folder> tree = [MakeFolder("inbox", FolderType.Inbox)];
        (FolderBarViewModel vm, _, _) = Build(tree);
        await vm.Load();
        bool raised = false;
        vm.FolderSelected += _ => raised = true;

        vm.DeselectFolder();

        Assert.False(raised);
    }

    /// <summary>DeselectFolder is a no-op when nothing is selected.</summary>
    [Fact]
    public void DeselectFolder_NothingSelected_IsNoOp()
    {
        (FolderBarViewModel vm, _, _) = Build();

        Exception? ex = Record.Exception(() => vm.DeselectFolder());

        Assert.Null(ex);
        Assert.Null(vm.SelectedFolder);
    }

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

    /// <summary>A received message may only move within the Inbox tree and a sent one within the Outbox tree, since each tree opens and deletes its entries as its own direction.</summary>
    [Theory]
    [InlineData(false, FolderType.Inbox, true)]
    [InlineData(false, FolderType.Outbox, false)]
    [InlineData(true, FolderType.Outbox, true)]
    [InlineData(true, FolderType.Inbox, false)]
    [InlineData(false, FolderType.Drafts, false)]
    [InlineData(true, FolderType.Notes, false)]
    [InlineData(false, FolderType.Activity, false)]
    public void IsCompatibleMove_MessageCompatibility(bool isOutbound, FolderType folderType, bool expected)
    {
        Assert.Equal(expected, FolderBarViewModel.IsCompatibleMove(EntryType.Message, folderType, isOutbound));
    }

    /// <summary>Drafts may only be moved into the Drafts folder.</summary>
    [Theory]
    [InlineData(FolderType.Drafts, true)]
    [InlineData(FolderType.Inbox, false)]
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

    private static async Task<(FolderBarViewModel Vm, Mock<IFolderRepository> Folders, Mock<IEntryService> Service, FolderItemViewModel Parent, FolderItemViewModel Child)> BuildWithSubfolder()
    {
        (FolderBarViewModel vm, Mock<IFolderRepository> folders, Mock<IEntryService> service) = Build([MakeFolder("notes", FolderType.Notes)]);
        await vm.Load();
        FolderItemViewModel parent = vm.RootFolders[0];
        await vm.AddSubfolder(parent, "Work");
        return (vm, folders, service, parent, parent.Children[0]);
    }

    /// <summary>Deleting a subfolder deletes its subfolders and the entries in every one of them, deepest first, and selects its parent when the selection was inside.</summary>
    [Fact]
    public async Task DeleteFolder_DeletesNestedSubfoldersAndTheirContents()
    {
        (FolderBarViewModel vm, Mock<IFolderRepository> folders, Mock<IEntryService> service, FolderItemViewModel parent, FolderItemViewModel child) = await BuildWithSubfolder();
        await vm.AddSubfolder(child, "Nested");
        FolderItemViewModel nested = child.Children[0];
        List<string> calls = [];
        service.Setup(s => s.DeleteFolderContents(It.IsAny<string>())).Callback<string>(id => calls.Add("contents:" + id)).Returns(Task.CompletedTask);
        folders.Setup(f => f.Delete(It.IsAny<string>())).Callback<string>(id => calls.Add("folder:" + id)).ReturnsAsync(true);
        Assert.Same(nested, vm.SelectedFolder);

        await vm.DeleteFolder(child);

        Assert.Equal([$"contents:{nested.Id}", $"folder:{nested.Id}", $"contents:{child.Id}", $"folder:{child.Id}"], calls);
        Assert.Empty(parent.Children);
        Assert.Same(parent, vm.SelectedFolder);
    }

    /// <summary>Deleting a folder the user is not inside leaves the selection alone.</summary>
    [Fact]
    public async Task DeleteFolder_SelectionElsewhere_IsKept()
    {
        (FolderBarViewModel vm, _, _, FolderItemViewModel parent, FolderItemViewModel child) = await BuildWithSubfolder();
        await vm.AddSubfolder(parent, "Other");
        FolderItemViewModel other = parent.Children[1];

        await vm.DeleteFolder(child);

        Assert.Same(other, vm.SelectedFolder);
    }

    /// <summary>A root folder is never deleted, and neither is any folder when the host forbids deleting entries of its type.</summary>
    [Fact]
    public async Task DeleteFolder_RootOrDeleteForbidden_DoesNothing()
    {
        (FolderBarViewModel vm, Mock<IFolderRepository> folders, Mock<IEntryService> service, FolderItemViewModel parent, FolderItemViewModel child) = await BuildWithSubfolder();

        Assert.False(vm.CanDeleteFolder(parent));
        Assert.True(vm.CanDeleteFolder(child));
        await vm.DeleteFolder(parent);

        service.Verify(s => s.DeleteFolderContents(It.IsAny<string>()), Times.Never);
        folders.Verify(f => f.Delete(It.IsAny<string>()), Times.Never);

        (FolderBarViewModel locked, Mock<IFolderRepository> lockedFolders, Mock<IEntryService> lockedService) = Build([MakeFolder("notes", FolderType.Notes)], canDelete: false);
        await locked.Load();
        await locked.AddSubfolder(locked.RootFolders[0], "Work");
        FolderItemViewModel sub = locked.RootFolders[0].Children[0];

        Assert.False(locked.CanDeleteFolder(sub));
        await locked.DeleteFolder(sub);

        lockedService.Verify(s => s.DeleteFolderContents(It.IsAny<string>()), Times.Never);
        lockedFolders.Verify(f => f.Delete(It.IsAny<string>()), Times.Never);
        Assert.Single(locked.RootFolders[0].Children);
    }

    /// <summary>While alerts are kept apart the inbox and the outbox each come with an alert root after them, listing the same stored folder, and neither alert root takes subfolders.</summary>
    [Fact]
    public async Task Load_AlertsSeparated_AddsAnAlertInboxAndAlertOutbox()
    {
        List<Folder> tree = [MakeFolder("inbox", FolderType.Inbox), MakeFolder("outbox", FolderType.Outbox), MakeFolder("drafts", FolderType.Drafts)];
        (FolderBarViewModel vm, _, _) = Build(tree, separateAlerts: true);

        await vm.Load();

        Assert.Equal(["inbox", "inbox-alerts", "outbox", "outbox-alerts", "drafts"], vm.RootFolders.Select(folder => folder.Id));
        Assert.Equal([false, true, false, true, null], vm.RootFolders.Select(folder => folder.AlertView));
        Assert.Equal("inbox", vm.RootFolders[1].StorageId);
        Assert.Equal(FolderType.Outbox, vm.RootFolders[3].RootType);
        Assert.False(vm.RootFolders[1].CanCreateSubfolder);
    }

    /// <summary>Without separation there are no alert roots.</summary>
    [Fact]
    public async Task Load_AlertsNotSeparated_HasNoAlertRoots()
    {
        List<Folder> tree = [MakeFolder("inbox", FolderType.Inbox), MakeFolder("outbox", FolderType.Outbox)];
        (FolderBarViewModel vm, _, _) = Build(tree);

        await vm.Load();

        Assert.Equal(["inbox", "outbox"], vm.RootFolders.Select(folder => folder.Id));
        Assert.All(vm.RootFolders, folder => Assert.Null(folder.AlertView));
    }
}
