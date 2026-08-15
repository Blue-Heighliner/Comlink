namespace BlueHeighliner.Comlink.Engine.Services;

/// <summary>Builds the reference list for a full export and writes selected entries to a zip archive as JSON files.</summary>
public interface IExportService
{
    /// <summary>
    /// File extension (including the leading dot) used for an export package, distinguishing it from an
    /// ordinary zip file so <see cref="IImportService"/> can find it on a drive.
    /// </summary>
    const string PackageExtension = ".export.zip";

    /// <summary>Returns a reference to every message, draft, note, and activity log entry in the database.</summary>
    Task<IReadOnlyList<ExportEntryRef>> GetAllEntryRefs();
    /// <summary>
    /// Writes each referenced entry to <paramref name="zipPath"/> as one JSON file per entry inside a new zip
    /// archive. If <paramref name="cancellation"/> is triggered, or the write otherwise fails, the partially
    /// written zip file at <paramref name="zipPath"/> is deleted before the exception propagates.
    /// </summary>
    /// <param name="entries">The entries to export.</param>
    /// <param name="zipPath">Absolute path of the zip file to create.</param>
    /// <param name="cancellation">Token used to cancel the export mid-write.</param>
    Task Export(IReadOnlyList<ExportEntryRef> entries, string zipPath, CancellationToken cancellation = default);
}

/// <summary>Builds the reference list for a full export and writes selected entries to a zip archive as JSON files.</summary>
public sealed class ExportService : IExportService
{
    private static DraftExportData BuildDraftExportData(DraftEntity entity) => new()
    {
        Id = entity.Id.ToString(),
        Subject = entity.Subject,
        Body = entity.Body,
        Addresses = entity.Addresses,
        IsSent = entity.IsSent,
        IsAlert = entity.IsAlert,
        Priority = entity.Priority,
        Tag = entity.Tag,
        SentAt = entity.SentAt,
        CreatedAt = entity.CreatedAt,
        ModifiedAt = entity.ModifiedAt
    };

    private static NoteExportData BuildNoteExportData(NoteEntity entity) => new()
    {
        Id = entity.Id.ToString(),
        Body = entity.Body,
        CreatedAt = entity.CreatedAt,
        ModifiedAt = entity.ModifiedAt
    };

    private static ActivityLogExportData BuildActivityLogExportData(ActivityLogEntity entity) => new()
    {
        Id = entity.Id.ToString(),
        Date = entity.Date,
        EventEntries = entity.EventEntries
    };

    private static string BuildEntryFileName(int index, ExportEntryRef entryRef)
        => $"{index:0000}_{entryRef.EntryType}_{SanitizeForFileName(entryRef.Id)}.json";

    private static string SanitizeForFileName(string value)
    {
        string result = value;
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(c, '_');
        }
        return result;
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static ObjectId? TryParseObjectId(string id)
    {
        try { return new ObjectId(id); }
        catch { return null; }
    }

    /// <summary>Initializes a new <see cref="ExportService"/> with the repositories and engine controller needed to read every entry type.</summary>
    public ExportService(
        IMessageRepository messages,
        IDraftRepository drafts,
        INoteRepository notes,
        IActivityLogRepository activityLogs,
        IEngineController engineController)
    {
        this.messages = messages;
        this.drafts = drafts;
        this.notes = notes;
        this.activityLogs = activityLogs;
        this.engineController = engineController;
    }

    private readonly IMessageRepository messages;
    private readonly IDraftRepository drafts;
    private readonly INoteRepository notes;
    private readonly IActivityLogRepository activityLogs;
    private readonly IEngineController engineController;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExportEntryRef>> GetAllEntryRefs()
    {
        List<ExportEntryRef> refs = [];

        foreach (MessageEntity m in await messages.GetAll())
        {
            refs.Add(new ExportEntryRef { Id = m.MessageId, EntryType = EntryType.Message, IsOutboundMessage = m.IsOutbound });
        }
        foreach (DraftEntity d in await drafts.GetAll())
        {
            refs.Add(new ExportEntryRef { Id = d.Id.ToString(), EntryType = EntryType.Draft });
        }
        foreach (NoteEntity n in await notes.GetAll())
        {
            refs.Add(new ExportEntryRef { Id = n.Id.ToString(), EntryType = EntryType.Note });
        }
        foreach (ActivityLogEntity a in await activityLogs.GetAll())
        {
            refs.Add(new ExportEntryRef { Id = a.Id.ToString(), EntryType = EntryType.Activity });
        }

        return refs;
    }

    /// <inheritdoc />
    public async Task Export(IReadOnlyList<ExportEntryRef> entries, string zipPath, CancellationToken cancellation = default)
    {
        try
        {
            using FileStream fs = new(zipPath, FileMode.Create, FileAccess.Write);
            using ZipArchive archive = new(fs, ZipArchiveMode.Create);

            int index = 0;
            foreach (ExportEntryRef entryRef in entries)
            {
                cancellation.ThrowIfCancellationRequested();

                object? data = await LoadExportData(entryRef);
                if (data is not null)
                {
                    ZipArchiveEntry zipEntry = archive.CreateEntry(BuildEntryFileName(index, entryRef), CompressionLevel.Optimal);
                    using Stream entryStream = zipEntry.Open();
                    await JsonSerializer.SerializeAsync(entryStream, data, data.GetType(), cancellationToken: cancellation);
                }
                index++;
            }
        }
        catch
        {
            TryDeleteFile(zipPath);
            throw;
        }
    }

    private async Task<object?> LoadExportData(ExportEntryRef entryRef)
    {
        switch (entryRef.EntryType)
        {
            case EntryType.Message:
                MessageEntity? message = await messages.Get(entryRef.Id, entryRef.IsOutboundMessage);
                return message is null ? null : BuildMessageExportData(message);

            case EntryType.Draft:
                ObjectId? draftId = TryParseObjectId(entryRef.Id);
                DraftEntity? draft = draftId is null ? null : await drafts.Get(draftId);
                return draft is null ? null : BuildDraftExportData(draft);

            case EntryType.Note:
                ObjectId? noteId = TryParseObjectId(entryRef.Id);
                NoteEntity? note = noteId is null ? null : await notes.Get(noteId);
                return note is null ? null : BuildNoteExportData(note);

            case EntryType.Activity:
                ObjectId? logId = TryParseObjectId(entryRef.Id);
                ActivityLogEntity? log = logId is null ? null : await activityLogs.Get(logId);
                return log is null ? null : BuildActivityLogExportData(log);

            default:
                return null;
        }
    }

    private MessageExportData BuildMessageExportData(MessageEntity entity) => new()
    {
        MessageId = entity.MessageId,
        IsOutbound = entity.IsOutbound,
        FromUser = engineController.GetFromUser(entity.Message),
        Subject = engineController.GetSubject(entity.Message),
        Body = engineController.GetBody(entity.Message),
        Addresses = engineController.GetAddresses(entity.Message)
            .Select(a => new AddressData { UserName = a.UserName, Type = a.Type.ToString() })
            .ToList(),
        SentAt = engineController.GetSentAt(entity.Message),
        IsAlert = engineController.GetIsAlert(entity.Message),
        Priority = engineController.GetPriority(entity.Message),
        Tag = engineController.GetTag(entity.Message),
        ReceivedAt = entity.ReceivedAt,
        ReadStatus = entity.ReadStatus,
        DeliveryStatuses = entity.DeliveryStatuses
    };
}
