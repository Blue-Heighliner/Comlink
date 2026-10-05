namespace BlueHeighliner.Comlink.Tests.Unit.Internal.ViewModels;

/// <summary>Unit tests for <see cref="NoteViewModel"/>.</summary>
public sealed class NoteViewModelTests
{
    private static NoteEntity MakeEntity(string body = "Initial body") => new()
    {
        Id = new ObjectId(),
        Body = body,
        FolderId = "root-notes",
        ModifiedAt = DateTime.UtcNow
    };

    /// <summary>Leaving a note saves what was written, but only when something changed, and never after the note was deleted.</summary>
    [Fact]
    public async Task SaveChanges_SavesOnlyWhatChanged()
    {
        Mock<IEntryService> svcMock = new();
        NoteViewModel vm = new(MakeEntity("same"), svcMock.Object, confirmationWindow: TimeSpan.FromMinutes(1));

        await vm.SaveChanges();
        svcMock.Verify(s => s.SaveNoteQuietly(It.IsAny<NoteEntity>()), Times.Never);

        vm.Body = "changed";
        await vm.SaveChanges();
        svcMock.Verify(s => s.SaveNoteQuietly(It.Is<NoteEntity>(note => note.Body == "changed")), Times.Once);

        await vm.DeleteCommand.ExecuteAsync(null);
        await vm.DeleteCommand.ExecuteAsync(null);
        vm.Body = "after delete";
        await vm.SaveChanges();
        svcMock.Verify(s => s.SaveNoteQuietly(It.IsAny<NoteEntity>()), Times.Once);
    }

    /// <summary>Id and Body are populated from the entity on construction.</summary>
    [Fact]
    public void Ctor_PopulatesIdAndBody()
    {
        NoteEntity entity = MakeEntity("Hello note");
        Mock<IEntryService> svcMock = new();

        NoteViewModel vm = new(entity, svcMock.Object);

        Assert.Equal(entity.Id.ToString(), vm.Id);
        Assert.Equal("Hello note", vm.Body);
        Assert.False(vm.IsSaving);
        Assert.Null(vm.StatusMessage);
    }

    /// <summary>DeleteCommand only arms on the first press, deleting nothing and leaving the button asking for confirmation.</summary>
    [Fact]
    public async Task Delete_FirstPress_ArmsWithoutDeleting()
    {
        Mock<IEntryService> svcMock = new();
        NoteViewModel vm = new(MakeEntity(), svcMock.Object, confirmationWindow: TimeSpan.FromMinutes(1));
        bool deleted = false;
        vm.Deleted += () => { deleted = true; return Task.CompletedTask; };

        Assert.Equal("DELETE", vm.DeleteButtonText);
        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.True(vm.IsConfirmingDelete);
        Assert.Equal("CONFIRM DELETE", vm.DeleteButtonText);
        Assert.False(deleted);
        svcMock.Verify(s => s.DeleteEntry(It.IsAny<string>(), It.IsAny<EntryType>(), It.IsAny<bool>()), Times.Never);
    }

    /// <summary>A confirming second press deletes the note through the entry service and raises Deleted.</summary>
    [Fact]
    public async Task Delete_SecondPress_DeletesNoteAndRaisesDeleted()
    {
        NoteEntity entity = MakeEntity();
        Mock<IEntryService> svcMock = new();
        NoteViewModel vm = new(entity, svcMock.Object, confirmationWindow: TimeSpan.FromMinutes(1));
        bool deleted = false;
        vm.Deleted += () => { deleted = true; return Task.CompletedTask; };

        await vm.DeleteCommand.ExecuteAsync(null);
        await vm.DeleteCommand.ExecuteAsync(null);

        svcMock.Verify(s => s.DeleteEntry(entity.Id.ToString(), EntryType.Note, false), Times.Once);
        Assert.True(deleted);
        Assert.False(vm.IsConfirmingDelete);
        Assert.Equal("DELETE", vm.DeleteButtonText);
    }

    /// <summary>When the host forbids deleting notes, the command does nothing however often it is pressed.</summary>
    [Fact]
    public async Task Delete_WhenNotAllowed_NeverDeletes()
    {
        Mock<IEntryService> svcMock = new();
        NoteViewModel vm = new(MakeEntity(), svcMock.Object, canDelete: false);

        await vm.DeleteCommand.ExecuteAsync(null);
        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.False(vm.CanDelete);
        Assert.False(vm.IsConfirmingDelete);
        svcMock.Verify(s => s.DeleteEntry(It.IsAny<string>(), It.IsAny<EntryType>(), It.IsAny<bool>()), Times.Never);
    }

    /// <summary>SaveCommand persists the current body via the entry service.</summary>
    [Fact]
    public async Task Save_PersistsBodyViaEntryService()
    {
        NoteEntity entity = MakeEntity();
        Mock<IEntryService> svcMock = new();
        svcMock.Setup(s => s.SaveNote(It.IsAny<NoteEntity>())).Returns(Task.CompletedTask);
        NoteViewModel vm = new(entity, svcMock.Object);
        vm.Body = "Updated body";

        await vm.SaveCommand.ExecuteAsync(null);

        svcMock.Verify(s => s.SaveNote(It.Is<NoteEntity>(e => e.Body == "Updated body")), Times.Once);
    }

    /// <summary>SaveCommand sets StatusMessage to "Saved" on success.</summary>
    [Fact]
    public async Task Save_OnSuccess_SetsStatusMessageToSaved()
    {
        Mock<IEntryService> svcMock = new();
        svcMock.Setup(s => s.SaveNote(It.IsAny<NoteEntity>())).Returns(Task.CompletedTask);
        NoteViewModel vm = new(MakeEntity(), svcMock.Object);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Saved", vm.StatusMessage);
    }

    /// <summary>IsSaving is true while SaveCommand is executing and false when done.</summary>
    [Fact]
    public async Task Save_IsSavingLifecycle()
    {
        TaskCompletionSource<bool> gate = new();
        Mock<IEntryService> svcMock = new();
        svcMock.Setup(s => s.SaveNote(It.IsAny<NoteEntity>())).Returns(gate.Task);
        NoteViewModel vm = new(MakeEntity(), svcMock.Object);

        Task saveTask = vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.IsSaving);

        gate.SetResult(true);
        await saveTask;
        Assert.False(vm.IsSaving);
    }

    /// <summary>A new note is not stored while unaltered or blank, and is inserted once it has content.</summary>
    [Fact]
    public async Task NewNote_IsStoredOnlyOnceAlteredAndNotBlank()
    {
        Mock<IEntryService> svcMock = new();
        NoteViewModel vm = new(MakeEntity(string.Empty), svcMock.Object, isNew: true);

        await vm.SaveChanges();
        vm.Body = "   ";
        await vm.SaveChanges();
        await vm.SaveCommand.ExecuteAsync(null);
        svcMock.Verify(s => s.InsertNote(It.IsAny<NoteEntity>()), Times.Never);
        svcMock.Verify(s => s.SaveNoteQuietly(It.IsAny<NoteEntity>()), Times.Never);
        Assert.Equal("Nothing to save", vm.StatusMessage);

        TaskCompletionSource inserted = new();
        svcMock.Setup(s => s.InsertNote(It.IsAny<NoteEntity>())).Returns(Task.CompletedTask).Callback(() => inserted.TrySetResult());
        vm.Body = "written";
        await inserted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await vm.SaveChanges();
        svcMock.Verify(s => s.InsertNote(It.Is<NoteEntity>(note => note.Body == "written")), Times.Once);
    }

    /// <summary>Deleting a note that was never stored removes nothing from the data store.</summary>
    [Fact]
    public async Task NewNote_Delete_DoesNotTouchTheStore()
    {
        Mock<IEntryService> svcMock = new();
        NoteViewModel vm = new(MakeEntity(string.Empty), svcMock.Object, confirmationWindow: TimeSpan.FromMinutes(1), isNew: true);

        await vm.DeleteCommand.ExecuteAsync(null);
        await vm.DeleteCommand.ExecuteAsync(null);

        svcMock.Verify(s => s.DeleteEntry(It.IsAny<string>(), It.IsAny<EntryType>(), It.IsAny<bool>()), Times.Never);
    }

    /// <summary>A name is saved trimmed, and clearing it saves null so the first line names the note again.</summary>
    [Fact]
    public async Task Name_IsSavedAndCleared()
    {
        Mock<IEntryService> svcMock = new();
        NoteEntity entity = MakeEntity("body");
        NoteViewModel vm = new(entity, svcMock.Object);

        vm.Name = "  Shopping  ";
        await vm.SaveChanges();
        Assert.Equal("Shopping", entity.Name);

        vm.Name = " ";
        await vm.SaveChanges();
        Assert.Null(entity.Name);
    }

    /// <summary>Duplicating copies the note as it is on screen, does not save the original, and reports the copy.</summary>
    [Fact]
    public async Task Duplicate_CopiesCurrentStateWithoutSavingTheOriginal()
    {
        Mock<IEntryService> svcMock = new();
        NoteEntity copy = MakeEntity("edited");
        svcMock.Setup(s => s.DuplicateNote(It.IsAny<NoteEntity>())).ReturnsAsync(copy);
        NoteViewModel vm = new(MakeEntity("original"), svcMock.Object);
        string? shown = null;
        vm.Duplicated += id =>
        {
            shown = id;
            return Task.CompletedTask;
        };

        vm.Body = "edited";
        await vm.DuplicateCommand.ExecuteAsync(null);

        svcMock.Verify(s => s.DuplicateNote(It.Is<NoteEntity>(note => note.Body == "edited")), Times.Once);
        svcMock.Verify(s => s.SaveNote(It.IsAny<NoteEntity>()), Times.Never);
        svcMock.Verify(s => s.SaveNoteQuietly(It.IsAny<NoteEntity>()), Times.Never);
        Assert.Equal(copy.Id.ToString(), shown);
    }
}
