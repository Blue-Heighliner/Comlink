namespace BlueHeighliner.Comlink.Tests.ViewModels;

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
}
