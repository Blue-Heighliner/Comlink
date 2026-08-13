namespace BlueHeighliner.Comlink.Tests.Services;

/// <summary>
/// Integration tests for <see cref="ImportService"/> using two separate real LiteDB databases (source and
/// destination) and real export packages built by <see cref="ExportService"/>, exercising a genuine
/// export-then-import round trip.
/// </summary>
public sealed class ImportServiceTests : IDisposable
{
    private static readonly IMessageFormat MessageFormat = new TestMessageFormat();

    private readonly string _sourceAppName = Guid.NewGuid().ToString();
    private readonly string _destAppName = Guid.NewGuid().ToString();
    private readonly string _packageDir = Path.Combine(Path.GetTempPath(), $"comlink-import-tests-{Guid.NewGuid():N}");
    private readonly LiteDbContext _sourceCtx;
    private readonly LiteDbContext _destCtx;
    private readonly MessageRepository _sourceMessages;
    private readonly DraftRepository _sourceDrafts;
    private readonly NoteRepository _sourceNotes;
    private readonly ActivityLogRepository _sourceActivityLogs;
    private readonly MessageRepository _destMessages;
    private readonly DraftRepository _destDrafts;
    private readonly NoteRepository _destNotes;
    private readonly ActivityLogRepository _destActivityLogs;
    private readonly FolderRepository _destFolders;
    private readonly ExportService _export;
    private readonly ImportService _import;

    /// <summary>Initializes two fresh isolated LiteDB databases (source and destination) and a temp package directory.</summary>
    public ImportServiceTests()
    {
        _sourceCtx = new LiteDbContext(new TestAppDataPathProvider(_sourceAppName));
        _sourceCtx.Initialize();
        _sourceMessages = new MessageRepository(_sourceCtx);
        _sourceDrafts = new DraftRepository(_sourceCtx);
        _sourceNotes = new NoteRepository(_sourceCtx);
        _sourceActivityLogs = new ActivityLogRepository(_sourceCtx);
        _export = new ExportService(_sourceMessages, _sourceDrafts, _sourceNotes, _sourceActivityLogs, MessageFormat);

        _destCtx = new LiteDbContext(new TestAppDataPathProvider(_destAppName));
        _destCtx.Initialize();
        _destMessages = new MessageRepository(_destCtx);
        _destDrafts = new DraftRepository(_destCtx);
        _destNotes = new NoteRepository(_destCtx);
        _destActivityLogs = new ActivityLogRepository(_destCtx);
        _destFolders = new FolderRepository(_destCtx);
        _import = new ImportService(_destMessages, _destDrafts, _destNotes, _destActivityLogs, _destFolders, MessageFormat);

        Directory.CreateDirectory(_packageDir);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _sourceCtx.Dispose();
        _destCtx.Dispose();
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        foreach (string appName in new[] { _sourceAppName, _destAppName })
        {
            string dir = Path.Combine(appData, appName);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        if (Directory.Exists(_packageDir)) Directory.Delete(_packageDir, recursive: true);
    }

    private static Task<DraftNoteConflictResolution> NeverAsked(ImportConflict conflict) =>
        throw new InvalidOperationException($"Unexpected conflict prompt for {conflict.EntryType} '{conflict.Name}'");

    private async Task<string> BuildPackage(params ExportEntryRef[] refs)
    {
        string path = Path.Combine(_packageDir, $"{Guid.NewGuid():N}{IExportService.PackageExtension}");
        await _export.Export(refs, path);
        return path;
    }

    private async Task<MessageEntity> InsertSourceMessage(string messageId, string subject, bool isOutbound, DateTime? receivedAt = null, int priority = 0)
    {
        object message = MessageFormat.CreateMessage();
        MessageFormat.SetMessageId(message, messageId);
        MessageFormat.SetSubject(message, subject);
        MessageFormat.SetPriority(message, priority);
        MessageEntity entity = new()
        {
            MessageId = messageId,
            Message = message,
            FolderId = "root-inbox",
            IsOutbound = isOutbound,
            ReceivedAt = receivedAt ?? DateTime.UtcNow
        };
        await _sourceMessages.Insert(entity);
        return entity;
    }

    // ── GetPackages ──────────────────────────────────────────────────────────

    /// <summary>GetPackages returns an empty list for a directory with no export packages.</summary>
    [Fact]
    public void GetPackages_NoPackages_ReturnsEmpty()
    {
        Assert.Empty(_import.GetPackages(_packageDir));
    }

    /// <summary>GetPackages finds only files ending in the export package extension, ignoring plain zips and other files.</summary>
    [Fact]
    public async Task GetPackages_FindsOnlyExportPackages()
    {
        await File.WriteAllTextAsync(Path.Combine(_packageDir, "notes.txt"), "hello");
        await File.WriteAllTextAsync(Path.Combine(_packageDir, "plain.zip"), "not a package");
        string packagePath = await BuildPackage();

        IReadOnlyList<ImportPackageInfo> packages = _import.GetPackages(_packageDir);

        ImportPackageInfo found = Assert.Single(packages);
        Assert.Equal(Path.GetFileName(packagePath), found.FileName);
        Assert.Equal(packagePath, found.FullPath);
    }

    /// <summary>GetPackages does not throw for a nonexistent directory.</summary>
    [Fact]
    public void GetPackages_NonexistentDirectory_ReturnsEmpty()
    {
        Assert.Empty(_import.GetPackages(Path.Combine(_packageDir, "does-not-exist")));
    }

    // ── Message import ───────────────────────────────────────────────────────

    /// <summary>A message with no existing counterpart in the destination is inserted.</summary>
    [Fact]
    public async Task Import_NewMessage_IsInserted()
    {
        await InsertSourceMessage("M1", "Hello", isOutbound: false, priority: 2);
        string package = await BuildPackage(new ExportEntryRef { Id = "M1", EntryType = EntryType.Message });

        ImportSummary summary = await _import.Import(package, NeverAsked);

        Assert.Equal(1, summary.Imported);
        Assert.Equal(0, summary.Skipped);
        MessageEntity? imported = await _destMessages.Get("M1", outbound: false);
        Assert.NotNull(imported);
        Assert.Equal("Hello", MessageFormat.GetSubject(imported.Message));
        Assert.Equal(2, MessageFormat.GetPriority(imported.Message));
    }

    /// <summary>A message matching an existing message's ID, direction, and date is skipped.</summary>
    [Fact]
    public async Task Import_DuplicateMessageIdAndDate_IsSkipped()
    {
        DateTime receivedAt = new(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        await InsertSourceMessage("M1", "Source Version", isOutbound: false, receivedAt);
        string package = await BuildPackage(new ExportEntryRef { Id = "M1", EntryType = EntryType.Message });

        await _destMessages.Insert(new MessageEntity
        {
            MessageId = "M1",
            Message = MessageFormat.CreateMessage(),
            FolderId = "root-inbox",
            IsOutbound = false,
            ReceivedAt = receivedAt
        });

        ImportSummary summary = await _import.Import(package, NeverAsked);

        Assert.Equal(0, summary.Imported);
        Assert.Equal(1, summary.Skipped);
    }

    /// <summary>Inbox and Outbox records for the same MessageId are treated as distinct — importing one does not skip the other.</summary>
    [Fact]
    public async Task Import_SameIdDifferentDirection_BothImported()
    {
        await InsertSourceMessage("M1", "In", isOutbound: false);
        await InsertSourceMessage("M1", "Out", isOutbound: true);
        string package = await BuildPackage(
            new ExportEntryRef { Id = "M1", EntryType = EntryType.Message, IsOutboundMessage = false },
            new ExportEntryRef { Id = "M1", EntryType = EntryType.Message, IsOutboundMessage = true });

        ImportSummary summary = await _import.Import(package, NeverAsked);

        Assert.Equal(2, summary.Imported);
    }

    // ── Draft import / conflicts ─────────────────────────────────────────────

    /// <summary>A draft with no existing entry of the same subject is inserted.</summary>
    [Fact]
    public async Task Import_NewDraft_IsInserted()
    {
        DraftEntity source = await _sourceDrafts.Insert(new DraftEntity { Subject = "Plan", Body = "Body", FolderId = "root-drafts", Priority = 2 });
        string package = await BuildPackage(new ExportEntryRef { Id = source.Id.ToString(), EntryType = EntryType.Draft });

        ImportSummary summary = await _import.Import(package, NeverAsked);

        Assert.Equal(1, summary.Imported);
        DraftEntity? imported = (await _destDrafts.GetAll()).SingleOrDefault(d => d.Subject == "Plan");
        Assert.NotNull(imported);
        Assert.Equal("Body", imported.Body);
        Assert.Equal(2, imported.Priority);
    }

    /// <summary>KeepExisting leaves the existing draft untouched and counts as skipped.</summary>
    [Fact]
    public async Task Import_DraftConflict_KeepExisting_PreservesExistingContent()
    {
        DraftEntity source = await _sourceDrafts.Insert(new DraftEntity { Subject = "Plan", Body = "New", FolderId = "root-drafts" });
        string package = await BuildPackage(new ExportEntryRef { Id = source.Id.ToString(), EntryType = EntryType.Draft });
        DraftEntity existing = await _destDrafts.Insert(new DraftEntity { Subject = "Plan", Body = "Old", FolderId = "root-drafts" });

        ImportSummary summary = await _import.Import(package, _ => Task.FromResult(DraftNoteConflictResolution.KeepExisting));

        Assert.Equal(0, summary.Imported);
        Assert.Equal(1, summary.Skipped);
        Assert.Equal(0, summary.Overwritten);
        DraftEntity? found = await _destDrafts.Get(existing.Id);
        Assert.Equal("Old", found!.Body);
    }

    /// <summary>Overwrite replaces the existing draft's content, keeping its identity.</summary>
    [Fact]
    public async Task Import_DraftConflict_Overwrite_ReplacesContent()
    {
        DraftEntity source = await _sourceDrafts.Insert(new DraftEntity { Subject = "Plan", Body = "New", FolderId = "root-drafts", Priority = 3 });
        string package = await BuildPackage(new ExportEntryRef { Id = source.Id.ToString(), EntryType = EntryType.Draft });
        DraftEntity existing = await _destDrafts.Insert(new DraftEntity { Subject = "Plan", Body = "Old", FolderId = "root-drafts", Priority = 0 });

        ImportSummary summary = await _import.Import(package, _ => Task.FromResult(DraftNoteConflictResolution.Overwrite));

        Assert.Equal(0, summary.Imported);
        Assert.Equal(1, summary.Overwritten);
        DraftEntity? found = await _destDrafts.Get(existing.Id);
        Assert.Equal("New", found!.Body);
        Assert.Equal(existing.Id, found.Id);
        Assert.Equal(3, found.Priority);
    }

    /// <summary>The conflict prompt receives the draft's subject as the conflict name.</summary>
    [Fact]
    public async Task Import_DraftConflict_PromptCarriesSubjectAndType()
    {
        DraftEntity source = await _sourceDrafts.Insert(new DraftEntity { Subject = "Plan", Body = "New", FolderId = "root-drafts" });
        string package = await BuildPackage(new ExportEntryRef { Id = source.Id.ToString(), EntryType = EntryType.Draft });
        await _destDrafts.Insert(new DraftEntity { Subject = "Plan", Body = "Old", FolderId = "root-drafts" });

        ImportConflict? seen = null;
        await _import.Import(package, c => { seen = c; return Task.FromResult(DraftNoteConflictResolution.KeepExisting); });

        Assert.NotNull(seen);
        Assert.Equal(EntryType.Draft, seen.EntryType);
        Assert.Equal("Plan", seen.Name);
    }

    /// <summary>OverwriteAll resolves the current conflict and every subsequent one without prompting again.</summary>
    [Fact]
    public async Task Import_OverwriteAll_AppliesToAllRemainingConflictsWithoutPrompting()
    {
        DraftEntity source1 = await _sourceDrafts.Insert(new DraftEntity { Subject = "A", Body = "New A", FolderId = "root-drafts" });
        DraftEntity source2 = await _sourceDrafts.Insert(new DraftEntity { Subject = "B", Body = "New B", FolderId = "root-drafts" });
        string package = await BuildPackage(
            new ExportEntryRef { Id = source1.Id.ToString(), EntryType = EntryType.Draft },
            new ExportEntryRef { Id = source2.Id.ToString(), EntryType = EntryType.Draft });
        DraftEntity existing1 = await _destDrafts.Insert(new DraftEntity { Subject = "A", Body = "Old A", FolderId = "root-drafts" });
        DraftEntity existing2 = await _destDrafts.Insert(new DraftEntity { Subject = "B", Body = "Old B", FolderId = "root-drafts" });

        int promptCount = 0;
        ImportSummary summary = await _import.Import(package, _ =>
        {
            promptCount++;
            return Task.FromResult(DraftNoteConflictResolution.OverwriteAll);
        });

        Assert.Equal(1, promptCount);
        Assert.Equal(2, summary.Overwritten);
        Assert.Equal("New A", (await _destDrafts.Get(existing1.Id))!.Body);
        Assert.Equal("New B", (await _destDrafts.Get(existing2.Id))!.Body);
    }

    // ── Note import / conflicts ──────────────────────────────────────────────

    /// <summary>Notes are matched by the first line of their body text.</summary>
    [Fact]
    public async Task Import_NoteConflict_MatchedByFirstLine()
    {
        NoteEntity source = await _sourceNotes.Insert(new NoteEntity { Body = "Groceries\nMilk\nEggs", FolderId = "root-notes" });
        string package = await BuildPackage(new ExportEntryRef { Id = source.Id.ToString(), EntryType = EntryType.Note });
        NoteEntity existing = await _destNotes.Insert(new NoteEntity { Body = "Groceries\nBread", FolderId = "root-notes" });

        ImportSummary summary = await _import.Import(package, _ => Task.FromResult(DraftNoteConflictResolution.Overwrite));

        Assert.Equal(1, summary.Overwritten);
        NoteEntity? found = await _destNotes.Get(existing.Id);
        Assert.Equal("Groceries\nMilk\nEggs", found!.Body);
    }

    /// <summary>A note with no matching first line is inserted as new.</summary>
    [Fact]
    public async Task Import_NewNote_IsInserted()
    {
        NoteEntity source = await _sourceNotes.Insert(new NoteEntity { Body = "Unique note", FolderId = "root-notes" });
        string package = await BuildPackage(new ExportEntryRef { Id = source.Id.ToString(), EntryType = EntryType.Note });

        ImportSummary summary = await _import.Import(package, NeverAsked);

        Assert.Equal(1, summary.Imported);
        Assert.Contains(await _destNotes.GetAll(), n => n.Body == "Unique note");
    }

    // ── Activity log merge ───────────────────────────────────────────────────

    /// <summary>An activity log for a date with no existing log is inserted as-is.</summary>
    [Fact]
    public async Task Import_ActivityLog_NoExisting_InsertsAsNew()
    {
        DateTime date = new(2025, 6, 1);
        ActivityLogEntity source = await _sourceActivityLogs.Insert(new ActivityLogEntity
        {
            Date = date,
            EventEntries = [new ActivityLogEntry { At = date.AddHours(10), Message = "A" }]
        });
        string package = await BuildPackage(new ExportEntryRef { Id = source.Id.ToString(), EntryType = EntryType.Activity });

        await _import.Import(package, NeverAsked);

        ActivityLogEntity? found = (await _destActivityLogs.GetAll()).SingleOrDefault(a => a.Date == date);
        Assert.NotNull(found);
        Assert.Single(found.EventEntries);
    }

    /// <summary>
    /// Merging into an existing log for the same date inserts new lines at the correct chronological
    /// position and skips any imported line that exactly matches an existing one.
    /// </summary>
    [Fact]
    public async Task Import_ActivityLog_MergesInTimestampOrderAndSkipsExactDuplicates()
    {
        DateTime date = new(2025, 6, 1);
        ActivityLogEntity source = await _sourceActivityLogs.Insert(new ActivityLogEntity
        {
            Date = date,
            EventEntries =
            [
                new ActivityLogEntry { At = date.AddHours(10), Message = "A" },   // exact duplicate of existing -> skipped
                new ActivityLogEntry { At = date.AddHours(11), Message = "B" },   // new -> inserted in the middle
                new ActivityLogEntry { At = date.AddHours(13), Message = "D" }    // new -> appended at the end
            ]
        });
        string package = await BuildPackage(new ExportEntryRef { Id = source.Id.ToString(), EntryType = EntryType.Activity });

        ActivityLogEntity existing = await _destActivityLogs.Insert(new ActivityLogEntity
        {
            Date = date,
            EventEntries =
            [
                new ActivityLogEntry { At = date.AddHours(10), Message = "A" },
                new ActivityLogEntry { At = date.AddHours(12), Message = "C" }
            ]
        });

        await _import.Import(package, NeverAsked);

        ActivityLogEntity merged = (await _destActivityLogs.Get(existing.Id))!;
        Assert.Equal(4, merged.EventEntries.Count);
        Assert.Equal(["A", "B", "C", "D"], merged.EventEntries.Select(e => e.Message).ToList());
    }

    /// <summary>A merged activity log line whose timestamp matches but message differs is not treated as a duplicate.</summary>
    [Fact]
    public async Task Import_ActivityLog_SameTimestampDifferentMessage_IsNotSkipped()
    {
        DateTime date = new(2025, 6, 1);
        DateTime at = date.AddHours(10);
        ActivityLogEntity source = await _sourceActivityLogs.Insert(new ActivityLogEntity
        {
            Date = date,
            EventEntries = [new ActivityLogEntry { At = at, Message = "Different" }]
        });
        string package = await BuildPackage(new ExportEntryRef { Id = source.Id.ToString(), EntryType = EntryType.Activity });

        ActivityLogEntity existing = await _destActivityLogs.Insert(new ActivityLogEntity
        {
            Date = date,
            EventEntries = [new ActivityLogEntry { At = at, Message = "Original" }]
        });

        await _import.Import(package, NeverAsked);

        ActivityLogEntity merged = (await _destActivityLogs.Get(existing.Id))!;
        Assert.Equal(2, merged.EventEntries.Count);
        Assert.Contains(merged.EventEntries, e => e.Message == "Original");
        Assert.Contains(merged.EventEntries, e => e.Message == "Different");
    }
}
