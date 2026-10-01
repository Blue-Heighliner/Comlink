namespace BlueHeighliner.Comlink.Services;

/// <summary>Builds the reference list for a full export and writes selected entries to a zip archive, in the built-in JSON format or a custom one (see <see cref="Control.IEngineController.ExportFormats"/>).</summary>
internal interface IExportService
{
    /// <summary>
    /// File extension (including the leading dot) used for an export package, distinguishing it from an
    /// ordinary zip file so <see cref="IImportService"/> can find it on a drive.
    /// </summary>
    const string PackageExtension = ".export.zip";

    /// <summary>Returns a reference to every message, draft, note, and activity log entry in the database.</summary>
    Task<IReadOnlyList<ExportEntryRef>> GetAllEntryRefs();
    /// <summary>
    /// Writes each referenced entry to <paramref name="zipPath"/> as one file per entry inside a new zip archive,
    /// using <paramref name="format"/>'s serializer, or the engine's own built-in JSON serializer when
    /// <paramref name="format"/> is <see langword="null"/>. An entry whose root folder type <paramref name="format"/>
    /// does not accept (see <see cref="Control.ExportFormatDefinition.AllowedTypes"/>) is left out, the same as one
    /// whose entity no longer exists. If <paramref name="cancellation"/> is triggered, or the write otherwise
    /// fails, the partially written zip file at <paramref name="zipPath"/> is deleted before the exception propagates.
    /// </summary>
    /// <param name="entries">The entries to export.</param>
    /// <param name="zipPath">Absolute path of the zip file to create.</param>
    /// <param name="format">The custom format to write with, or <see langword="null"/> for the built-in JSON format.</param>
    /// <param name="cancellation">Token used to cancel the export mid-write.</param>
    /// <returns>How many entries were actually written - may be fewer than <paramref name="entries"/>.Count, per the rules above.</returns>
    Task<int> Export(IReadOnlyList<ExportEntryRef> entries, string zipPath, ExportFormatDefinition? format = null, CancellationToken cancellation = default);
}

/// <inheritdoc cref="IExportService" />
internal sealed class ExportService : IExportService
{
    private static DraftExportData BuildDraftExportData(DraftEntity entity) => new()
    {
        Id = entity.Id.ToString(),
        Body = entity.Body,
        BodySegmentsJson = entity.BodySegmentsJson,
        Addresses = [.. entity.Addresses.Select(a => new AddressRequest { UserName = a.UserName, Type = a.Type, Information = a.Information })],
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
        EventEntries = [.. entity.EventEntries.Select(e => new ActivityLogEventEntry { At = e.At, Message = e.Message })]
    };

    private static FolderType ToFolderType(ExportEntryRef entryRef) => entryRef.EntryType switch
    {
        EntryType.Message => entryRef.IsOutboundMessage ? FolderType.Outbox : FolderType.Inbox,
        EntryType.Draft => FolderType.Drafts,
        EntryType.Note => FolderType.Notes,
        _ => FolderType.Activity
    };

    private static string BuildEntryFileName(int index, ExportEntryRef entryRef, ExportFormatDefinition? format)
        => $"{index:0000}_{entryRef.EntryType}_{SanitizeForFileName(entryRef.Id)}.{FileExtension(format)}";

    private static string FileExtension(ExportFormatDefinition? format)
    {
        if (format is null) { return "json"; }

        string sanitized = new([.. format.Name.Where(char.IsLetterOrDigit)]);
        return sanitized.Length > 0 ? sanitized.ToLowerInvariant() : "dat";
    }

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
        try { if (File.Exists(path)) { File.Delete(path); } } catch { }
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
    public async Task<int> Export(IReadOnlyList<ExportEntryRef> entries, string zipPath, ExportFormatDefinition? format = null, CancellationToken cancellation = default)
    {
        // Written beside the target and moved into place only once complete, so a cancelled or failed export never
        // destroys an existing package of the same name.
        string tempPath = zipPath + ".partial";
        try
        {
            int written = 0;
            await using (FileStream fs = new(tempPath, FileMode.Create, FileAccess.Write))
            await using (ZipArchive archive = new(fs, ZipArchiveMode.Create))
            {
                int index = 0;
                foreach (ExportEntryRef entryRef in entries)
                {
                    cancellation.ThrowIfCancellationRequested();

                    if (format?.AllowedTypes is { } allowed && !allowed(ToFolderType(entryRef)))
                    {
                        index++;
                        continue;
                    }

                    object? data = await LoadExportData(entryRef);
                    if (data is not null)
                    {
                        ZipArchiveEntry zipEntry = archive.CreateEntry(BuildEntryFileName(index, entryRef, format), CompressionLevel.Optimal);
                        await using Stream entryStream = await zipEntry.OpenAsync(cancellation);
                        if (format is null) { await JsonSerializer.SerializeAsync(entryStream, data, data.GetType(), cancellationToken: cancellation); }
                        else { await format.Serialize(data, entryStream, cancellation); }
                        written++;
                    }
                    index++;
                }
            }

            File.Move(tempPath, zipPath, overwrite: true);
            return written;
        }
        catch
        {
            TryDeleteFile(tempPath);
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
        Body = engineController.GetBody(entity.Message),
        Addresses = engineController.GetAddresses(entity.Message)
            .Select(a => new AddressRequest { UserName = a.UserName, Type = a.Type.ToString(), Information = a.Information })
            .ToList(),
        SentAt = engineController.GetSentAt(entity.Message),
        IsAlert = engineController.GetIsAlert(entity.Message),
        Priority = engineController.GetPriority(entity.Message),
        Tag = engineController.GetTag(entity.Message),
        ReceivedAt = entity.ReceivedAt,
        ReadStatus = entity.ReadStatus,
        DeliveryStatuses = [.. entity.DeliveryStatuses.Select(d => new MessageDeliveryStatus { UserName = d.UserName, Status = d.Status, AddressedVia = d.AddressedVia })]
    };
}
