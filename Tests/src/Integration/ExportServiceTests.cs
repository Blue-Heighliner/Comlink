namespace BlueHeighliner.Comlink.Tests.Integration;

/// <summary>Integration tests for <see cref="ExportService"/> using a real LiteDB database and real zip files on disk.</summary>
public sealed class ExportServiceTests : IDisposable
{
    /// <summary>Initializes a fresh isolated LiteDB context and temp export directory for each test.</summary>
    public ExportServiceTests()
    {
        ctx = new LiteDbContext(new TestAppDataPathProvider(appName));
        ctx.Initialize();
        messages = new MessageRepository(ctx);
        drafts = new DraftRepository(ctx);
        notes = new NoteRepository(ctx);
        activityLogs = new ActivityLogRepository(ctx);
        service = new ExportService(messages, drafts, notes, activityLogs, messageFormat);
        Directory.CreateDirectory(exportDir);
    }

    private readonly IEngineController messageFormat = new TestEngineController();
    private readonly string appName = Guid.NewGuid().ToString();
    private readonly string exportDir = Path.Combine(Path.GetTempPath(), $"comlink-export-tests-{Guid.NewGuid():N}");
    private readonly LiteDbContext ctx;
    private readonly MessageRepository messages;
    private readonly DraftRepository drafts;
    private readonly NoteRepository notes;
    private readonly ActivityLogRepository activityLogs;
    private readonly ExportService service;

    /// <inheritdoc />
    public void Dispose()
    {
        ctx.Dispose();
        string dbDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
        if (Directory.Exists(dbDir))
        {
            Directory.Delete(dbDir, recursive: true);
        }
        if (Directory.Exists(exportDir))
        {
            Directory.Delete(exportDir, recursive: true);
        }
    }

    private string ZipPath() => Path.Combine(exportDir, "export" + IExportService.PackageExtension);

    private async Task<MessageEntity> InsertMessage(string messageId, string body, bool isOutbound, int priority = 0)
    {
        MessageData message = new();
        message.Id = messageId;
        message.Body = body;
        message.Priority = priority;
        MessageEntity entity = new() { MessageId = messageId, Message = message, FolderId = "root-inbox", IsOutbound = isOutbound };
        await messages.Insert(entity);
        return entity;
    }

    /// <summary>GetAllEntryRefs returns one reference per message, draft, note, and activity log document.</summary>
    [Fact]
    public async Task GetAllEntryRefs_ReturnsRefForEveryEntryType()
    {
        MessageEntity message = await InsertMessage("M1", "Hello", isOutbound: false);
        DraftEntity draft = await drafts.Insert(new DraftEntity { Body = "D", FolderId = "root-drafts" });
        NoteEntity note = await notes.Insert(new NoteEntity { Body = "N", FolderId = "root-notes" });
        ActivityLogEntity log = await activityLogs.Insert(new ActivityLogEntity { Date = DateTime.UtcNow.Date });

        IReadOnlyList<ExportEntryRef> refs = await service.GetAllEntryRefs();

        Assert.Equal(4, refs.Count);
        Assert.Contains(refs, r => r.Id == message.MessageId && r.EntryType is EntryType.Message && !r.IsOutboundMessage);
        Assert.Contains(refs, r => r.Id == draft.Id.ToString() && r.EntryType is EntryType.Draft);
        Assert.Contains(refs, r => r.Id == note.Id.ToString() && r.EntryType is EntryType.Note);
        Assert.Contains(refs, r => r.Id == log.Id.ToString() && r.EntryType is EntryType.Activity);
    }

    /// <summary>GetAllEntryRefs disambiguates Inbox and Outbox records for a self-addressed message sharing a MessageId.</summary>
    [Fact]
    public async Task GetAllEntryRefs_SelfAddressedMessage_ReturnsBothDirections()
    {
        await InsertMessage("M1", "In", isOutbound: false);
        await InsertMessage("M1", "Out", isOutbound: true);

        IReadOnlyList<ExportEntryRef> refs = await service.GetAllEntryRefs();

        Assert.Equal(2, refs.Count);
        Assert.Contains(refs, r => r.Id == "M1" && !r.IsOutboundMessage);
        Assert.Contains(refs, r => r.Id == "M1" && r.IsOutboundMessage);
    }

    /// <summary>Export writes one JSON file per entry into the zip, with content matching the source entity.</summary>
    [Fact]
    public async Task Export_WritesOneJsonFilePerEntry()
    {
        await InsertMessage("M1", "Hello World", isOutbound: false, priority: 3);
        DraftEntity draft = await drafts.Insert(new DraftEntity { Body = "Draft Subject", FolderId = "root-drafts", Priority = 2 });
        string zipPath = ZipPath();

        List<ExportEntryRef> refs =
        [
            new ExportEntryRef { Id = "M1", EntryType = EntryType.Message, IsOutboundMessage = false },
            new ExportEntryRef { Id = draft.Id.ToString(), EntryType = EntryType.Draft }
        ];

        int written = await service.Export(refs, zipPath);

        Assert.Equal(2, written);
        Assert.True(File.Exists(zipPath));
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        Assert.Equal(2, archive.Entries.Count);

        ZipArchiveEntry messageEntry = Assert.Single(archive.Entries, e => e.Name.Contains("Message"));
        using (StreamReader reader = new(messageEntry.Open()))
        {
            MessageExportData? data = JsonSerializer.Deserialize<MessageExportData>(reader.ReadToEnd());
            Assert.Equal("Hello World", data!.Body);
            Assert.Equal(3, data.Priority);
        }

        ZipArchiveEntry draftEntry = Assert.Single(archive.Entries, e => e.Name.Contains("Draft"));
        using (StreamReader reader = new(draftEntry.Open()))
        {
            DraftExportData? data = JsonSerializer.Deserialize<DraftExportData>(reader.ReadToEnd());
            Assert.Equal("Draft Subject", data!.Body);
            Assert.Equal(2, data.Priority);
        }
    }

    /// <summary>Export skips a reference whose entity no longer exists, without throwing.</summary>
    [Fact]
    public async Task Export_ReferenceToMissingEntity_IsSkipped()
    {
        string zipPath = ZipPath();
        List<ExportEntryRef> refs = [new ExportEntryRef { Id = "does-not-exist", EntryType = EntryType.Message, IsOutboundMessage = false }];

        int written = await service.Export(refs, zipPath);

        Assert.Equal(0, written);
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        Assert.Empty(archive.Entries);
    }

    /// <summary>Export with a custom format calls its serializer instead of the built-in JSON serializer, and names each entry's file with an extension derived from the format's name.</summary>
    [Fact]
    public async Task Export_CustomFormat_UsesItsSerializerAndFileExtension()
    {
        await InsertMessage("M1", "Hello", isOutbound: false);
        string zipPath = ZipPath();
        List<ExportEntryRef> refs = [new ExportEntryRef { Id = "M1", EntryType = EntryType.Message, IsOutboundMessage = false }];
        List<object> serialized = [];
        ExportFormatDefinition format = new()
        {
            Name = "CSV",
            Serialize = async (entry, stream, cancellation) =>
            {
                serialized.Add(entry);
                await using StreamWriter writer = new(stream, leaveOpen: true);
                await writer.WriteAsync("custom output");
            }
        };

        int written = await service.Export(refs, zipPath, format);

        Assert.Equal(1, written);
        Assert.Single(serialized);
        Assert.IsType<MessageExportData>(Assert.Single(serialized));
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        ZipArchiveEntry entry = Assert.Single(archive.Entries);
        Assert.EndsWith(".csv", entry.Name);
        using StreamReader reader = new(entry.Open());
        Assert.Equal("custom output", await reader.ReadToEndAsync());
    }

    /// <summary>Export with a custom format restricted to certain root folder types leaves out an entry of a type it does not accept, without calling its serializer for it, and reports the reduced written count.</summary>
    [Fact]
    public async Task Export_CustomFormatWithEntryTypeFilter_SkipsDisallowedTypes()
    {
        await InsertMessage("M1", "Hello", isOutbound: false);
        DraftEntity draft = await drafts.Insert(new DraftEntity { Body = "D", FolderId = "root-drafts" });
        string zipPath = ZipPath();
        List<ExportEntryRef> refs =
        [
            new ExportEntryRef { Id = "M1", EntryType = EntryType.Message, IsOutboundMessage = false },
            new ExportEntryRef { Id = draft.Id.ToString(), EntryType = EntryType.Draft }
        ];
        int serializeCalls = 0;
        ExportFormatDefinition format = new()
        {
            Name = "MessagesOnly",
            Serialize = async (entry, stream, cancellation) =>
            {
                serializeCalls++;
                await JsonSerializer.SerializeAsync(stream, entry, entry.GetType(), cancellationToken: cancellation);
            },
            AllowedTypes = type => type is FolderType.Inbox or FolderType.Outbox
        };

        int written = await service.Export(refs, zipPath, format);

        Assert.Equal(1, written);
        Assert.Equal(1, serializeCalls);
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        ZipArchiveEntry entry = Assert.Single(archive.Entries);
        Assert.Contains("Message", entry.Name);
    }

    /// <summary>Export with an empty reference list still creates a valid (empty) zip file.</summary>
    [Fact]
    public async Task Export_NoEntries_CreatesEmptyZip()
    {
        string zipPath = ZipPath();

        await service.Export([], zipPath);

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

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.Export(refs, zipPath, cancellation: cts.Token));

        Assert.False(File.Exists(zipPath));
    }

    /// <summary>A cancelled export leaves an existing package of the same name exactly as it was, and leaves no partial file behind.</summary>
    [Fact]
    public async Task Export_Cancelled_KeepsExistingPackage()
    {
        MessageEntity message = await InsertMessage(Guid.NewGuid().ToString("N"), "Subject", isOutbound: false);
        ExportEntryRef[] refs = [new ExportEntryRef { Id = message.MessageId, EntryType = EntryType.Message }];
        await service.Export(refs, ZipPath());
        byte[] original = await File.ReadAllBytesAsync(ZipPath());
        using CancellationTokenSource cts = new();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.Export(refs, ZipPath(), cancellation: cts.Token));

        Assert.Equal(original, await File.ReadAllBytesAsync(ZipPath()));
        Assert.Equal([ZipPath()], Directory.GetFiles(exportDir));
    }
}
