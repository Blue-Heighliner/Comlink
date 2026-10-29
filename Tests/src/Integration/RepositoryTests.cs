namespace BlueHeighliner.Comlink.Tests.Integration;

/// <summary>Integration tests for all repository classes using a real LiteDB database.</summary>
public sealed class RepositoryTests : IDisposable
{
    private static MessageEntity MakeMessage(string messageId, string folderId, bool isOutbound)
    {
        MessageData message = new();
        message.Id = messageId;
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
        string dir = TestAppDataPathProvider.DirectoryOf(appName);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>AppendEvent creates a new entity for today when none exists.</summary>
    [Fact]
    public async Task ActivityLog_AppendEvent_CreatesEntityForToday()
    {
        ActivityLogRepository repo = new(ctx);
        await repo.AppendEvent("first event", "CL7");

        ActivityLogEntity? today = (await repo.GetAll()).SingleOrDefault();
        Assert.NotNull(today);
        Assert.Single(today.EventEntries);
        Assert.Equal("first event", today.EventEntries[0].Message);
        Assert.Equal("CL7", today.EventEntries[0].EventId);
    }

    /// <summary>What is logged before the database is open waits, and is written with the time it was logged once the database opens.</summary>
    [Fact]
    public async Task ActivityLog_AppendEventBeforeTheDatabaseIsOpen_IsWrittenOnceItOpens()
    {
        string appName = Guid.NewGuid().ToString();
        LiteDbContext closed = new(new TestAppDataPathProvider(appName));
        try
        {
            ActivityLogRepository repo = new(closed);
            DateTime before = DateTime.UtcNow;

            await repo.AppendEvent("starting", "CL65");
            await repo.AppendEvent("started", "CL66");
            Assert.False(closed.IsOpen);

            closed.Initialize();
            DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while ((await repo.GetAll()).Count == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            ActivityLogEntity today = Assert.Single(await repo.GetAll());
            Assert.Equal(["starting", "started"], today.EventEntries.Select(entry => entry.Message));
            Assert.Equal(["CL65", "CL66"], today.EventEntries.Select(entry => entry.EventId));
            Assert.All(today.EventEntries, entry => Assert.InRange(entry.At.ToUniversalTime(), before.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1)));

            await repo.AppendEvent("later", "CL67");
            Assert.Equal(3, (await repo.GetAll()).Single().EventEntries.Count);
        }
        finally
        {
            closed.Dispose();
            string dir = TestAppDataPathProvider.DirectoryOf(appName);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    /// <summary>Appending twice adds to the same day's entity.</summary>
    [Fact]
    public async Task ActivityLog_AppendEvent_AccumulatesOnSameDay()
    {
        ActivityLogRepository repo = new(ctx);
        await repo.AppendEvent("A", "CL1");
        await repo.AppendEvent("B", "CL2");

        ActivityLogEntity? today = (await repo.GetAll()).SingleOrDefault();
        Assert.Equal(2, today!.EventEntries.Count);
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
