namespace BlueHeighliner.Comlink.Tests.Services;

/// <summary>Integration tests for <see cref="ExportService"/> using a real LiteDB database and real zip files on disk.</summary>
public sealed class ExportServiceTests : IDisposable
{
    private static readonly IMessageFormat MessageFormat = new TestMessageFormat();

    private readonly string _appName = Guid.NewGuid().ToString();
    private readonly string _exportDir = Path.Combine(Path.GetTempPath(), $"comlink-export-tests-{Guid.NewGuid():N}");
    private readonly LiteDbContext _ctx;
    private readonly MessageRepository _messages;
    private readonly DraftRepository _drafts;
    private readonly NoteRepository _notes;
    private readonly ActivityLogRepository _activityLogs;
    private readonly ExportService _service;

    /// <summary>Initializes a fresh isolated LiteDB context and temp export directory for each test.</summary>
    public ExportServiceTests()
    {
        _ctx = new LiteDbContext(new TestAppDataPathProvider(_appName));
        _ctx.Initialize();
        _messages = new MessageRepository(_ctx);
        _drafts = new DraftRepository(_ctx);
        _notes = new NoteRepository(_ctx);
        _activityLogs = new ActivityLogRepository(_ctx);
        _service = new ExportService(_messages, _drafts, _notes, _activityLogs, MessageFormat);
        Directory.CreateDirectory(_exportDir);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _ctx.Dispose();
        string dbDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), _appName);
        if (Directory.Exists(dbDir)) Directory.Delete(dbDir, recursive: true);
        if (Directory.Exists(_exportDir)) Directory.Delete(_exportDir, recursive: true);
    }

    private string ZipPath() => Path.Combine(_exportDir, "export" + IExportService.PackageExtension);

    private async Task<MessageEntity> InsertMessage(string messageId, string subject, bool isOutbound, int priority = 0)
    {
        object message = MessageFormat.CreateMessage();
        MessageFormat.SetMessageId(message, messageId);
        MessageFormat.SetSubject(message, subject);
        MessageFormat.SetPriority(message, priority);
        MessageEntity entity = new() { MessageId = messageId, Message = message, FolderId = "root-inbox", IsOutbound = isOutbound };
        await _messages.Insert(entity);
        return entity;
    }

    // ── GetAllEntryRefs ──────────────────────────────────────────────────────

    /// <summary>GetAllEntryRefs returns one reference per message, draft, note, and activity log document.</summary>
    [Fact]
    public async Task GetAllEntryRefs_ReturnsRefForEveryEntryType()
    {
        MessageEntity message = await InsertMessage("M1", "Hello", isOutbound: false);
        DraftEntity draft = await _drafts.Insert(new DraftEntity { Subject = "D", FolderId = "root-drafts" });
        NoteEntity note = await _notes.Insert(new NoteEntity { Body = "N", FolderId = "root-notes" });
        ActivityLogEntity log = await _activityLogs.Insert(new ActivityLogEntity { Date = DateTime.UtcNow.Date });

        IReadOnlyList<ExportEntryRef> refs = await _service.GetAllEntryRefs();

        Assert.Equal(4, refs.Count);
        Assert.Contains(refs, r => r.Id == message.MessageId && r.EntryType == EntryType.Message && !r.IsOutboundMessage);
        Assert.Contains(refs, r => r.Id == draft.Id.ToString() && r.EntryType == EntryType.Draft);
        Assert.Contains(refs, r => r.Id == note.Id.ToString() && r.EntryType == EntryType.Note);
        Assert.Contains(refs, r => r.Id == log.Id.ToString() && r.EntryType == EntryType.Activity);
    }

    /// <summary>GetAllEntryRefs disambiguates Inbox and Outbox records for a self-addressed message sharing a MessageId.</summary>
    [Fact]
    public async Task GetAllEntryRefs_SelfAddressedMessage_ReturnsBothDirections()
    {
        await InsertMessage("M1", "In", isOutbound: false);
        await InsertMessage("M1", "Out", isOutbound: true);

        IReadOnlyList<ExportEntryRef> refs = await _service.GetAllEntryRefs();

        Assert.Equal(2, refs.Count);
        Assert.Contains(refs, r => r.Id == "M1" && !r.IsOutboundMessage);
        Assert.Contains(refs, r => r.Id == "M1" && r.IsOutboundMessage);
    }

    // ── Export ───────────────────────────────────────────────────────────────

    /// <summary>Export writes one JSON file per entry into the zip, with content matching the source entity.</summary>
    [Fact]
    public async Task Export_WritesOneJsonFilePerEntry()
    {
        await InsertMessage("M1", "Hello World", isOutbound: false, priority: 3);
        DraftEntity draft = await _drafts.Insert(new DraftEntity { Subject = "Draft Subject", FolderId = "root-drafts", Priority = 2 });
        string zipPath = ZipPath();

        List<ExportEntryRef> refs =
        [
            new ExportEntryRef { Id = "M1", EntryType = EntryType.Message, IsOutboundMessage = false },
            new ExportEntryRef { Id = draft.Id.ToString(), EntryType = EntryType.Draft }
        ];

        await _service.Export(refs, zipPath);

        Assert.True(File.Exists(zipPath));
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        Assert.Equal(2, archive.Entries.Count);

        ZipArchiveEntry messageEntry = Assert.Single(archive.Entries, e => e.Name.Contains("Message"));
        using (StreamReader reader = new(messageEntry.Open()))
        {
            MessageExportData? data = JsonSerializer.Deserialize<MessageExportData>(reader.ReadToEnd());
            Assert.Equal("Hello World", data!.Subject);
            Assert.Equal(3, data.Priority);
        }

        ZipArchiveEntry draftEntry = Assert.Single(archive.Entries, e => e.Name.Contains("Draft"));
        using (StreamReader reader = new(draftEntry.Open()))
        {
            DraftExportData? data = JsonSerializer.Deserialize<DraftExportData>(reader.ReadToEnd());
            Assert.Equal("Draft Subject", data!.Subject);
            Assert.Equal(2, data.Priority);
        }
    }

    /// <summary>Export skips a reference whose entity no longer exists, without throwing.</summary>
    [Fact]
    public async Task Export_ReferenceToMissingEntity_IsSkipped()
    {
        string zipPath = ZipPath();
        List<ExportEntryRef> refs = [new ExportEntryRef { Id = "does-not-exist", EntryType = EntryType.Message, IsOutboundMessage = false }];

        await _service.Export(refs, zipPath);

        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        Assert.Empty(archive.Entries);
    }

    /// <summary>Export with an empty reference list still creates a valid (empty) zip file.</summary>
    [Fact]
    public async Task Export_NoEntries_CreatesEmptyZip()
    {
        string zipPath = ZipPath();

        await _service.Export([], zipPath);

        Assert.True(File.Exists(zipPath));
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        Assert.Empty(archive.Entries);
    }

    /// <summary>A pre-cancelled token aborts the export and deletes the partially written zip file.</summary>
    [Fact]
    public async Task Export_Cancelled_DeletesPartialZipFile()
    {
        await InsertMessage("M1", "Hello", isOutbound: false);
        string zipPath = ZipPath();
        List<ExportEntryRef> refs = [new ExportEntryRef { Id = "M1", EntryType = EntryType.Message, IsOutboundMessage = false }];

        using CancellationTokenSource cts = new();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _service.Export(refs, zipPath, cts.Token));

        Assert.False(File.Exists(zipPath));
    }
}
