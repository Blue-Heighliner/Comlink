namespace BlueHeighliner.Comlink.Tests.Data;

/// <summary>Integration tests for all repository classes using a real LiteDB database.</summary>
public sealed class RepositoryTests : IDisposable
{
    private readonly string _appName = Guid.NewGuid().ToString();
    private readonly LiteDbContext _ctx;

    /// <summary>Initializes a fresh isolated LiteDB context for each test.</summary>
    public RepositoryTests()
    {
        _ctx = new LiteDbContext(new TestAppDataPathProvider(_appName));
        _ctx.Initialize();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _ctx.Dispose();
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), _appName);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    // ── ActivityLogRepository ─────────────────────────────────────────────────

    /// <summary>AppendEvent creates a new entity for today when none exists.</summary>
    [Fact]
    public async Task ActivityLog_AppendEvent_CreatesEntityForToday()
    {
        ActivityLogRepository repo = new(_ctx);
        await repo.AppendEvent("first event");

        ActivityLogEntity? today = await repo.GetForToday();
        Assert.NotNull(today);
        Assert.Single(today.EventEntries);
        Assert.Equal("first event", today.EventEntries[0].Message);
    }

    /// <summary>Appending twice adds to the same day's entity.</summary>
    [Fact]
    public async Task ActivityLog_AppendEvent_AccumulatesOnSameDay()
    {
        ActivityLogRepository repo = new(_ctx);
        await repo.AppendEvent("A");
        await repo.AppendEvent("B");

        ActivityLogEntity? today = await repo.GetForToday();
        Assert.Equal(2, today!.EventEntries.Count);
    }

    /// <summary>Insert then Get by id returns the entity.</summary>
    [Fact]
    public async Task ActivityLog_Insert_ThenGetById_ReturnsEntity()
    {
        ActivityLogRepository repo = new(_ctx);
        ActivityLogEntity entity = new() { Date = DateTime.UtcNow.Date, EventEntries = [] };
        await repo.Insert(entity);

        ActivityLogEntity? found = await repo.Get(entity.Id);
        Assert.NotNull(found);
        Assert.Equal(entity.Id, found.Id);
    }

    /// <summary>Update persists changes to an existing entity.</summary>
    [Fact]
    public async Task ActivityLog_Update_PersistsChanges()
    {
        ActivityLogRepository repo = new(_ctx);
        ActivityLogEntity entity = new() { Date = DateTime.UtcNow.Date, EventEntries = [] };
        await repo.Insert(entity);
        entity.EventEntries.Add(new ActivityLogEntry { At = DateTime.UtcNow, Message = "updated" });
        await repo.Update(entity);

        ActivityLogEntity? found = await repo.Get(entity.Id);
        Assert.Single(found!.EventEntries);
        Assert.Equal("updated", found.EventEntries[0].Message);
    }

    /// <summary>GetPage returns entities ordered by date descending.</summary>
    [Fact]
    public async Task ActivityLog_GetPage_ReturnsInDescendingDateOrder()
    {
        ActivityLogRepository repo = new(_ctx);
        ActivityLogEntity older = new() { Date = DateTime.UtcNow.Date.AddDays(-1), EventEntries = [] };
        ActivityLogEntity newer = new() { Date = DateTime.UtcNow.Date, EventEntries = [] };
        await repo.Insert(older);
        await repo.Insert(newer);

        List<ActivityLogEntity> page = await repo.GetPage(1);

        Assert.Equal(2, page.Count);
        Assert.True(page[0].Date >= page[1].Date);
    }

    /// <summary>Count returns the total number of activity log documents.</summary>
    [Fact]
    public async Task ActivityLog_Count_ReturnsCorrectTotal()
    {
        ActivityLogRepository repo = new(_ctx);
        await repo.Insert(new ActivityLogEntity { Date = DateTime.UtcNow.Date, EventEntries = [] });
        await repo.Insert(new ActivityLogEntity { Date = DateTime.UtcNow.Date.AddDays(-1), EventEntries = [] });

        int count = await repo.Count();
        Assert.Equal(2, count);
    }

    // ── DraftRepository ───────────────────────────────────────────────────────

    private static DraftEntity MakeDraft(string folderId, string subject = "Sub", bool sent = false, DateTime? modifiedAt = null) =>
        new() { FolderId = folderId, Subject = subject, IsSent = sent, ModifiedAt = modifiedAt ?? DateTime.UtcNow };

    /// <summary>Insert then Get by id returns the draft.</summary>
    [Fact]
    public async Task Draft_Insert_ThenGet_ReturnsDraft()
    {
        DraftRepository repo = new(_ctx);
        DraftEntity draft = MakeDraft("root-drafts");
        await repo.Insert(draft);

        DraftEntity? found = await repo.Get(draft.Id);
        Assert.NotNull(found);
        Assert.Equal(draft.Id, found.Id);
    }

    /// <summary>GetPage returns unsent drafts alphabetically when alphabetical=true.</summary>
    [Fact]
    public async Task Draft_GetPage_Alphabetical_ReturnsInSubjectOrder()
    {
        DraftRepository repo = new(_ctx);
        await repo.Insert(MakeDraft("root-drafts", "Zebra"));
        await repo.Insert(MakeDraft("root-drafts", "Alpha"));
        await repo.Insert(MakeDraft("root-drafts", "Mango"));

        List<DraftEntity> page = await repo.GetPage("root-drafts", 1, alphabetical: true);

        Assert.Equal(["Alpha", "Mango", "Zebra"], page.Select(d => d.Subject).ToList());
    }

    /// <summary>GetPage returns unsent drafts newest-first when alphabetical=false.</summary>
    [Fact]
    public async Task Draft_GetPage_Chronological_ReturnsNewestFirst()
    {
        DraftRepository repo = new(_ctx);
        DateTime now = DateTime.UtcNow;
        await repo.Insert(MakeDraft("root-drafts", "Old", modifiedAt: now.AddHours(-2)));
        await repo.Insert(MakeDraft("root-drafts", "New", modifiedAt: now));

        List<DraftEntity> page = await repo.GetPage("root-drafts", 1, alphabetical: false);

        Assert.Equal("New", page[0].Subject);
        Assert.Equal("Old", page[1].Subject);
    }

    /// <summary>GetPage excludes sent drafts.</summary>
    [Fact]
    public async Task Draft_GetPage_ExcludesSentDrafts()
    {
        DraftRepository repo = new(_ctx);
        await repo.Insert(MakeDraft("root-drafts", "Unsent"));
        await repo.Insert(MakeDraft("root-drafts", "Sent", sent: true));

        List<DraftEntity> page = await repo.GetPage("root-drafts", 1, alphabetical: true);

        Assert.Single(page);
        Assert.Equal("Unsent", page[0].Subject);
    }

    /// <summary>Count returns count of unsent drafts in the folder.</summary>
    [Fact]
    public async Task Draft_Count_ReturnsUnsentCount()
    {
        DraftRepository repo = new(_ctx);
        await repo.Insert(MakeDraft("root-drafts", "A"));
        await repo.Insert(MakeDraft("root-drafts", "B"));
        await repo.Insert(MakeDraft("root-drafts", "C", sent: true));

        int count = await repo.Count("root-drafts");
        Assert.Equal(2, count);
    }

    /// <summary>Update persists changes to a draft.</summary>
    [Fact]
    public async Task Draft_Update_PersistsSubjectChange()
    {
        DraftRepository repo = new(_ctx);
        DraftEntity draft = MakeDraft("root-drafts", "Original");
        await repo.Insert(draft);
        draft.Subject = "Updated";
        await repo.Update(draft);

        DraftEntity? found = await repo.Get(draft.Id);
        Assert.Equal("Updated", found!.Subject);
    }

    /// <summary>Delete removes the draft from the database.</summary>
    [Fact]
    public async Task Draft_Delete_RemovesDraft()
    {
        DraftRepository repo = new(_ctx);
        DraftEntity draft = MakeDraft("root-drafts");
        await repo.Insert(draft);
        await repo.Delete(draft.Id);

        DraftEntity? found = await repo.Get(draft.Id);
        Assert.Null(found);
    }

    // ── FolderRepository ──────────────────────────────────────────────────────

    private static FolderEntity MakeFolder(string id, FolderType type, string? parentId = null) =>
        new() { Id = id, Name = type.ToString(), RootType = type, ParentId = parentId };

    /// <summary>GetRootId returns "root-{type}".</summary>
    [Theory]
    [InlineData(FolderType.Inbox,    "root-inbox")]
    [InlineData(FolderType.Outbox,   "root-outbox")]
    [InlineData(FolderType.Drafts,   "root-drafts")]
    [InlineData(FolderType.Notes,    "root-notes")]
    [InlineData(FolderType.Activity, "root-activity")]
    public async Task Folder_GetRootId_ReturnsExpectedId(FolderType type, string expected)
    {
        FolderRepository repo = new(_ctx);
        string id = await repo.GetRootId(type);
        Assert.Equal(expected, id);
    }

    /// <summary>Insert then GetAll returns inserted folders.</summary>
    [Fact]
    public async Task Folder_Insert_ThenGetAll_ContainsInserted()
    {
        FolderRepository repo = new(_ctx);
        FolderEntity f = MakeFolder("inbox-root", FolderType.Inbox);
        await repo.Insert(f);

        List<FolderEntity> all = await repo.GetAll();
        Assert.Contains(all, x => x.Id == "inbox-root");
    }

    /// <summary>Get by id returns the folder.</summary>
    [Fact]
    public async Task Folder_Get_ById_ReturnsFolder()
    {
        FolderRepository repo = new(_ctx);
        FolderEntity f = MakeFolder("notes-root", FolderType.Notes);
        await repo.Insert(f);

        FolderEntity? found = await repo.Get("notes-root");
        Assert.NotNull(found);
        Assert.Equal(FolderType.Notes, found.RootType);
    }

    /// <summary>Delete returns true and removes the folder.</summary>
    [Fact]
    public async Task Folder_Delete_RemovesFolder()
    {
        FolderRepository repo = new(_ctx);
        FolderEntity f = MakeFolder("del-folder", FolderType.Drafts);
        await repo.Insert(f);

        bool deleted = await repo.Delete("del-folder");
        Assert.True(deleted);
        Assert.Null(await repo.Get("del-folder"));
    }

    /// <summary>GetTree builds a hierarchy with root and children.</summary>
    [Fact]
    public async Task Folder_GetTree_BuildsHierarchy()
    {
        FolderRepository repo = new(_ctx);
        FolderEntity root = MakeFolder("root", FolderType.Inbox);
        FolderEntity child = MakeFolder("child", FolderType.Inbox, "root");
        await repo.Insert(root);
        await repo.Insert(child);

        List<Folder> tree = await repo.GetTree();

        Folder? rootFolder = tree.FirstOrDefault(f => f.Id == "root");
        Assert.NotNull(rootFolder);
        Assert.Single(rootFolder.Children);
        Assert.Equal("child", rootFolder.Children[0].Id);
    }

    // ── NoteRepository ────────────────────────────────────────────────────────

    private static NoteEntity MakeNote(string folderId, string body = "Note", DateTime? modifiedAt = null) =>
        new() { FolderId = folderId, Body = body, ModifiedAt = modifiedAt ?? DateTime.UtcNow };

    /// <summary>Insert then Get returns the note.</summary>
    [Fact]
    public async Task Note_Insert_ThenGet_ReturnsNote()
    {
        NoteRepository repo = new(_ctx);
        NoteEntity note = MakeNote("root-notes");
        await repo.Insert(note);

        NoteEntity? found = await repo.Get(note.Id);
        Assert.NotNull(found);
        Assert.Equal(note.Id, found.Id);
    }

    /// <summary>GetPage alphabetical returns notes sorted by body text.</summary>
    [Fact]
    public async Task Note_GetPage_Alphabetical_SortsByBody()
    {
        NoteRepository repo = new(_ctx);
        await repo.Insert(MakeNote("root-notes", "Zebra"));
        await repo.Insert(MakeNote("root-notes", "Apple"));

        List<NoteEntity> page = await repo.GetPage("root-notes", 1, alphabetical: true);

        Assert.Equal("Apple", page[0].Body);
        Assert.Equal("Zebra", page[1].Body);
    }

    /// <summary>GetPage chronological returns newest notes first.</summary>
    [Fact]
    public async Task Note_GetPage_Chronological_SortsByModifiedAtDescending()
    {
        NoteRepository repo = new(_ctx);
        DateTime now = DateTime.UtcNow;
        await repo.Insert(MakeNote("root-notes", "Old", modifiedAt: now.AddHours(-1)));
        await repo.Insert(MakeNote("root-notes", "New", modifiedAt: now));

        List<NoteEntity> page = await repo.GetPage("root-notes", 1, alphabetical: false);

        Assert.Equal("New", page[0].Body);
    }

    /// <summary>Count returns count of notes in the folder.</summary>
    [Fact]
    public async Task Note_Count_ReturnsCorrectCount()
    {
        NoteRepository repo = new(_ctx);
        await repo.Insert(MakeNote("root-notes"));
        await repo.Insert(MakeNote("root-notes"));
        await repo.Insert(MakeNote("other-folder"));

        int count = await repo.Count("root-notes");
        Assert.Equal(2, count);
    }

    /// <summary>Update persists body changes.</summary>
    [Fact]
    public async Task Note_Update_PersistsChanges()
    {
        NoteRepository repo = new(_ctx);
        NoteEntity note = MakeNote("root-notes", "Original");
        await repo.Insert(note);
        note.Body = "Updated";
        await repo.Update(note);

        NoteEntity? found = await repo.Get(note.Id);
        Assert.Equal("Updated", found!.Body);
    }

    /// <summary>Delete removes the note.</summary>
    [Fact]
    public async Task Note_Delete_RemovesNote()
    {
        NoteRepository repo = new(_ctx);
        NoteEntity note = MakeNote("root-notes");
        await repo.Insert(note);
        await repo.Delete(note.Id);

        Assert.Null(await repo.Get(note.Id));
    }
}
