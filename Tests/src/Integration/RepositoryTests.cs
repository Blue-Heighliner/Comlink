namespace BlueHeighliner.Comlink.Tests.Integration;

/// <summary>Integration tests for all repository classes using a real LiteDB database.</summary>
public sealed class RepositoryTests : IDisposable
{
    private static MessageEntity MakeMessage(string messageId, string folderId, bool isOutbound)
    {
        object message = messageFormat.CreateFrame();
        ((TestFrame)message).MessageId = messageId;
        return new MessageEntity { MessageId = messageId, Message = message, FolderId = folderId, IsOutbound = isOutbound };
    }

    private static DraftEntity MakeDraft(string folderId, string body = "Sub", bool sent = false, DateTime? modifiedAt = null)
        => new() { FolderId = folderId, Body = body, IsSent = sent, ModifiedAt = modifiedAt ?? DateTime.UtcNow };

    private static FolderEntity MakeFolder(string id, FolderType type, string? parentId = null)
        => new() { Id = id, Name = type.ToString(), RootType = type, ParentId = parentId };

    private static NoteEntity MakeNote(string folderId, string body = "Note", DateTime? modifiedAt = null)
        => new() { FolderId = folderId, Body = body, ModifiedAt = modifiedAt ?? DateTime.UtcNow };

    private static readonly IEngineController messageFormat = new TestEngineController();

    /// <summary>Initializes a fresh isolated LiteDB context for each test.</summary>
    public RepositoryTests()
    {
        ctx = new LiteDbContext(new TestAppDataPathProvider(appName));
        ctx.Initialize();
    }

    private readonly string appName = Guid.NewGuid().ToString();
    private readonly LiteDbContext ctx;

    /// <inheritdoc />
    public void Dispose()
    {
        ctx.Dispose();
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
        if (Directory.Exists(dir)) { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>AppendEvent creates a new entity for today when none exists.</summary>
    [Fact]
    public async Task ActivityLog_AppendEvent_CreatesEntityForToday()
    {
        ActivityLogRepository repo = new(ctx);
        await repo.AppendEvent("first event", 7);

        ActivityLogEntity? today = (await repo.GetAll()).SingleOrDefault();
        Assert.NotNull(today);
        Assert.Single(today.EventEntries);
        Assert.Equal("first event", today.EventEntries[0].Message);
        Assert.Equal(7, today.EventEntries[0].EventId);
    }

    /// <summary>What is logged before the database is open waits, and is written with the time it was logged once the database opens.</summary>
    [Fact]
    public async Task ActivityLog_AppendEventBeforeTheDatabaseIsOpen_IsWrittenOnceItOpens()
    {
        LiteDbContext closed = new(new TestAppDataPathProvider(Guid.NewGuid().ToString()));
        try
        {
            ActivityLogRepository repo = new(closed);
            DateTime before = DateTime.UtcNow;

            await repo.AppendEvent("starting", 65);
            await repo.AppendEvent("started", 66);
            Assert.False(closed.IsOpen);

            closed.Initialize();
            DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while ((await repo.GetAll()).Count == 0 && DateTime.UtcNow < deadline) { await Task.Delay(20); }

            ActivityLogEntity today = Assert.Single(await repo.GetAll());
            Assert.Equal(["starting", "started"], today.EventEntries.Select(entry => entry.Message));
            Assert.Equal([65, 66], today.EventEntries.Select(entry => entry.EventId));
            Assert.All(today.EventEntries, entry => Assert.InRange(entry.At.ToUniversalTime(), before.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1)));

            await repo.AppendEvent("later", 67);
            Assert.Equal(3, (await repo.GetAll()).Single().EventEntries.Count);
        }
        finally
        {
            closed.Dispose();
        }
    }

    /// <summary>Appending twice adds to the same day's entity.</summary>
    [Fact]
    public async Task ActivityLog_AppendEvent_AccumulatesOnSameDay()
    {
        ActivityLogRepository repo = new(ctx);
        await repo.AppendEvent("A", 1);
        await repo.AppendEvent("B", 2);

        ActivityLogEntity? today = (await repo.GetAll()).SingleOrDefault();
        Assert.Equal(2, today!.EventEntries.Count);
    }

    /// <summary>Insert then Get by id returns the entity.</summary>
    [Fact]
    public async Task ActivityLog_Insert_ThenGetById_ReturnsEntity()
    {
        ActivityLogRepository repo = new(ctx);
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
        ActivityLogRepository repo = new(ctx);
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
        ActivityLogRepository repo = new(ctx);
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
        ActivityLogRepository repo = new(ctx);
        await repo.Insert(new ActivityLogEntity { Date = DateTime.UtcNow.Date, EventEntries = [] });
        await repo.Insert(new ActivityLogEntity { Date = DateTime.UtcNow.Date.AddDays(-1), EventEntries = [] });

        int count = await repo.Count();
        Assert.Equal(2, count);
    }

    /// <summary>Insert then Get by id returns the draft.</summary>
    [Fact]
    public async Task Draft_Insert_ThenGet_ReturnsDraft()
    {
        DraftRepository repo = new(ctx);
        DraftEntity draft = MakeDraft("root-drafts");
        await repo.Insert(draft);

        DraftEntity? found = await repo.Get(draft.Id);
        Assert.NotNull(found);
        Assert.Equal(draft.Id, found.Id);
    }

    /// <summary>GetPage returns unsent drafts alphabetically when alphabetical=true.</summary>
    [Fact]
    public async Task Draft_GetPage_Alphabetical_ReturnsInBodyOrder()
    {
        DraftRepository repo = new(ctx);
        await repo.Insert(MakeDraft("root-drafts", "Zebra"));
        await repo.Insert(MakeDraft("root-drafts", "Alpha"));
        await repo.Insert(MakeDraft("root-drafts", "Mango"));

        List<DraftEntity> page = await repo.GetPage("root-drafts", 1, alphabetical: true);

        Assert.Equal(["Alpha", "Mango", "Zebra"], page.Select(d => d.Body).ToList());
    }

    /// <summary>GetPage returns unsent drafts newest-first when alphabetical=false.</summary>
    [Fact]
    public async Task Draft_GetPage_Chronological_ReturnsNewestFirst()
    {
        DraftRepository repo = new(ctx);
        DateTime now = DateTime.UtcNow;
        await repo.Insert(MakeDraft("root-drafts", "Old", modifiedAt: now.AddHours(-2)));
        await repo.Insert(MakeDraft("root-drafts", "New", modifiedAt: now));

        List<DraftEntity> page = await repo.GetPage("root-drafts", 1, alphabetical: false);

        Assert.Equal("New", page[0].Body);
        Assert.Equal("Old", page[1].Body);
    }

    /// <summary>GetPage excludes sent drafts.</summary>
    [Fact]
    public async Task Draft_GetPage_ExcludesSentDrafts()
    {
        DraftRepository repo = new(ctx);
        await repo.Insert(MakeDraft("root-drafts", "Unsent"));
        await repo.Insert(MakeDraft("root-drafts", "Sent", sent: true));

        List<DraftEntity> page = await repo.GetPage("root-drafts", 1, alphabetical: true);

        Assert.Single(page);
        Assert.Equal("Unsent", page[0].Body);
    }

    /// <summary>Count returns count of unsent drafts in the folder.</summary>
    [Fact]
    public async Task Draft_Count_ReturnsUnsentCount()
    {
        DraftRepository repo = new(ctx);
        await repo.Insert(MakeDraft("root-drafts", "A"));
        await repo.Insert(MakeDraft("root-drafts", "B"));
        await repo.Insert(MakeDraft("root-drafts", "C", sent: true));

        int count = await repo.Count("root-drafts");
        Assert.Equal(2, count);
    }

    /// <summary>Update persists changes to a draft.</summary>
    [Fact]
    public async Task Draft_Update_PersistsBodyChange()
    {
        DraftRepository repo = new(ctx);
        DraftEntity draft = MakeDraft("root-drafts", "Original");
        await repo.Insert(draft);
        draft.Body = "Updated";
        await repo.Update(draft);

        DraftEntity? found = await repo.Get(draft.Id);
        Assert.Equal("Updated", found!.Body);
    }

    /// <summary>Delete removes the draft from the database.</summary>
    [Fact]
    public async Task Draft_Delete_RemovesDraft()
    {
        DraftRepository repo = new(ctx);
        DraftEntity draft = MakeDraft("root-drafts");
        await repo.Insert(draft);
        await repo.Delete(draft.Id);

        DraftEntity? found = await repo.Get(draft.Id);
        Assert.Null(found);
    }

    /// <summary>GetRootId returns "root-{type}".</summary>
    [Theory]
    [InlineData(FolderType.Inbox, "root-inbox")]
    [InlineData(FolderType.Outbox, "root-outbox")]
    [InlineData(FolderType.Drafts, "root-drafts")]
    [InlineData(FolderType.Notes, "root-notes")]
    [InlineData(FolderType.Activity, "root-activity")]
    public async Task Folder_GetRootId_ReturnsExpectedId(FolderType type, string expected)
    {
        FolderRepository repo = new(ctx);
        string id = await repo.GetRootId(type);
        Assert.Equal(expected, id);
    }

    /// <summary>Insert then GetAll returns inserted folders.</summary>
    [Fact]
    public async Task Folder_Insert_ThenGetAll_ContainsInserted()
    {
        FolderRepository repo = new(ctx);
        FolderEntity f = MakeFolder("inbox-root", FolderType.Inbox);
        await repo.Insert(f);

        List<FolderEntity> all = await repo.GetAll();
        Assert.Contains(all, x => x.Id == "inbox-root");
    }

    /// <summary>Get by id returns the folder.</summary>
    [Fact]
    public async Task Folder_Get_ById_ReturnsFolder()
    {
        FolderRepository repo = new(ctx);
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
        FolderRepository repo = new(ctx);
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
        FolderRepository repo = new(ctx);
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

    /// <summary>Insert then Get returns the note.</summary>
    [Fact]
    public async Task Note_Insert_ThenGet_ReturnsNote()
    {
        NoteRepository repo = new(ctx);
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
        NoteRepository repo = new(ctx);
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
        NoteRepository repo = new(ctx);
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
        NoteRepository repo = new(ctx);
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
        NoteRepository repo = new(ctx);
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
        NoteRepository repo = new(ctx);
        NoteEntity note = MakeNote("root-notes");
        await repo.Insert(note);
        await repo.Delete(note.Id);

        Assert.Null(await repo.Get(note.Id));
    }

    /// <summary>MessageRepository.GetAll returns messages from every folder, both Inbox and Outbox.</summary>
    [Fact]
    public async Task Message_GetAll_ReturnsAllFoldersAndDirections()
    {
        MessageRepository repo = new(ctx);
        await repo.Insert(MakeMessage("M1", "root-inbox", isOutbound: false));
        await repo.Insert(MakeMessage("M2", "root-outbox", isOutbound: true));
        await repo.Insert(MakeMessage("M3", "custom-folder", isOutbound: false));

        List<MessageEntity> all = await repo.GetAll();

        Assert.Equal(3, all.Count);
        Assert.Contains(all, m => m.MessageId == "M1");
        Assert.Contains(all, m => m.MessageId == "M2");
        Assert.Contains(all, m => m.MessageId == "M3");
    }

    /// <summary>DraftRepository.GetAll returns both sent and unsent drafts across all folders.</summary>
    [Fact]
    public async Task Draft_GetAll_ReturnsSentAndUnsentAcrossFolders()
    {
        DraftRepository repo = new(ctx);
        await repo.Insert(MakeDraft("root-drafts", "Unsent"));
        await repo.Insert(MakeDraft("root-drafts", "Sent", sent: true));
        await repo.Insert(MakeDraft("custom-folder", "Other"));

        List<DraftEntity> all = await repo.GetAll();

        Assert.Equal(3, all.Count);
    }

    /// <summary>NoteRepository.GetAll returns notes across all folders.</summary>
    [Fact]
    public async Task Note_GetAll_ReturnsAllFolders()
    {
        NoteRepository repo = new(ctx);
        await repo.Insert(MakeNote("root-notes"));
        await repo.Insert(MakeNote("custom-folder"));

        List<NoteEntity> all = await repo.GetAll();

        Assert.Equal(2, all.Count);
    }

    /// <summary>ActivityLogRepository.GetAll returns every activity log document.</summary>
    [Fact]
    public async Task ActivityLog_GetAll_ReturnsAllDocuments()
    {
        ActivityLogRepository repo = new(ctx);
        await repo.Insert(new ActivityLogEntity { Date = DateTime.UtcNow.Date, EventEntries = [] });
        await repo.Insert(new ActivityLogEntity { Date = DateTime.UtcNow.Date.AddDays(-1), EventEntries = [] });

        List<ActivityLogEntity> all = await repo.GetAll();

        Assert.Equal(2, all.Count);
    }

    /// <summary>AutoForwardTargetsRepository.Get returns null for a controller that has never been saved.</summary>
    [Fact]
    public async Task AutoForwardTargets_Get_NeverSaved_ReturnsNull()
    {
        AutoForwardTargetsRepository repo = new(ctx);

        Assert.Null(await repo.Get("Alerts"));
    }

    /// <summary>Save then Get round-trips the target list under the controller's own name.</summary>
    [Fact]
    public async Task AutoForwardTargets_Save_ThenGet_RoundTrips()
    {
        AutoForwardTargetsRepository repo = new(ctx);
        await repo.Save("Alerts", ["ALICE", "BOB"]);

        AutoForwardTargetsEntity? found = await repo.Get("Alerts");

        Assert.NotNull(found);
        Assert.Equal("Alerts", found.Id);
        Assert.Equal(["ALICE", "BOB"], found.Targets);
    }

    /// <summary>Saving again for the same controller replaces its target list rather than creating a second document.</summary>
    [Fact]
    public async Task AutoForwardTargets_Save_Twice_ReplacesTargetList()
    {
        AutoForwardTargetsRepository repo = new(ctx);
        await repo.Save("Alerts", ["ALICE"]);

        await repo.Save("Alerts", ["BOB"]);

        AutoForwardTargetsEntity? found = await repo.Get("Alerts");
        Assert.Equal(["BOB"], found!.Targets);
    }

    /// <summary>Different controllers keep independent target lists.</summary>
    [Fact]
    public async Task AutoForwardTargets_Save_DifferentControllers_AreIndependent()
    {
        AutoForwardTargetsRepository repo = new(ctx);
        await repo.Save("Alerts", ["ALICE"]);
        await repo.Save("Backups", ["BOB"]);

        Assert.Equal(["ALICE"], (await repo.Get("Alerts"))!.Targets);
        Assert.Equal(["BOB"], (await repo.Get("Backups"))!.Targets);
    }

    /// <summary>The last identifier is null until one is saved, then the latest one saved is returned, also by a new repository over the same database.</summary>
    [Fact]
    public async Task LastId_Save_ThenGet_ReturnsTheLatest()
    {
        LastIdRepository repo = new(ctx);
        Assert.Null(await repo.Get());

        await repo.Save("A1");
        await repo.Save("A2");

        Assert.Equal("A2", await repo.Get());
        Assert.Equal("A2", await new LastIdRepository(ctx).Get());
    }
}
