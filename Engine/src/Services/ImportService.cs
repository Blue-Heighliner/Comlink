namespace BlueHeighliner.Comlink.Engine.Services;

/// <summary>Lists export packages on a drive and restores their entries into the local database.</summary>
public interface IImportService
{
    /// <summary>Returns every <see cref="IExportService.PackageExtension"/> package found directly under <paramref name="driveRootPath"/>, ordered by file name.</summary>
    IReadOnlyList<ImportPackageInfo> GetPackages(string driveRootPath);

    /// <summary>
    /// Restores every entry in the package at <paramref name="packagePath"/>:
    /// <list type="bullet">
    /// <item>A message matching an existing message's ID, direction, and date is skipped.</item>
    /// <item>
    /// A draft/note matching an existing entry's name (subject, or note first line) invokes
    /// <paramref name="resolveConflict"/> to ask how to proceed, unless a prior conflict in this same call
    /// was resolved as <see cref="DraftNoteConflictResolution.OverwriteAll"/>, in which case it is
    /// overwritten without asking.
    /// </item>
    /// <item>An activity log matching an existing log's date is merged into it line by line, skipping any imported line that exactly matches an existing one and inserting the rest in timestamp order.</item>
    /// </list>
    /// </summary>
    /// <param name="packagePath">Absolute path of the package to import.</param>
    /// <param name="resolveConflict">Invoked once per unresolved draft/note name conflict to obtain the user's choice.</param>
    Task<ImportSummary> Import(string packagePath, Func<ImportConflict, Task<DraftNoteConflictResolution>> resolveConflict);
}

/// <summary>Lists export packages on a drive and restores their entries into the local database.</summary>
public sealed class ImportService : IImportService
{
    private static string FirstLine(string? body) => (body ?? string.Empty).Split('\n').FirstOrDefault()?.Trim() ?? string.Empty;

    private static EntryType? ParseEntryType(string fileName)
    {
        string[] parts = fileName.Split('_', 3);
        return parts.Length >= 2 && Enum.TryParse(parts[1], out EntryType type) ? type : null;
    }

    private static async Task<T?> ReadEntry<T>(ZipArchiveEntry zipEntry)
    {
        using Stream stream = zipEntry.Open();
        return await JsonSerializer.DeserializeAsync<T>(stream);
    }

    /// <summary>Initializes a new <see cref="ImportService"/> with the repositories and engine controller needed to restore every entry type.</summary>
    public ImportService(
        IMessageRepository messages,
        IDraftRepository drafts,
        INoteRepository notes,
        IActivityLogRepository activityLogs,
        IFolderRepository folders,
        IEngineController engineController)
    {
        this.messages = messages;
        this.drafts = drafts;
        this.notes = notes;
        this.activityLogs = activityLogs;
        this.folders = folders;
        this.engineController = engineController;
    }

    private readonly IMessageRepository messages;
    private readonly IDraftRepository drafts;
    private readonly INoteRepository notes;
    private readonly IActivityLogRepository activityLogs;
    private readonly IFolderRepository folders;
    private readonly IEngineController engineController;

    /// <inheritdoc />
    public IReadOnlyList<ImportPackageInfo> GetPackages(string driveRootPath)
    {
        try
        {
            return Directory.GetFiles(driveRootPath, $"*{IExportService.PackageExtension}")
                .Select(path => new ImportPackageInfo { FileName = Path.GetFileName(path), FullPath = path })
                .OrderBy(p => p.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<ImportSummary> Import(string packagePath, Func<ImportConflict, Task<DraftNoteConflictResolution>> resolveConflict)
    {
        int imported = 0;
        int skipped = 0;
        int overwritten = 0;
        bool overwriteAll = false;

        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        foreach (ZipArchiveEntry zipEntry in archive.Entries)
        {
            EntryType? entryType = ParseEntryType(zipEntry.Name);
            if (entryType is null) { continue; }

            switch (entryType)
            {
                case EntryType.Message:
                {
                    if (await ImportMessage(zipEntry)) { imported++; }
                    else { skipped++; }
                    break;
                }
                case EntryType.Draft:
                {
                    (bool wasImported, bool wasOverwritten) = await ImportDraft(zipEntry, resolveConflict, () => overwriteAll, v => overwriteAll = v);
                    if (wasOverwritten) { overwritten++; }
                    else if (wasImported) { imported++; }
                    else { skipped++; }
                    break;
                }
                case EntryType.Note:
                {
                    (bool wasImported, bool wasOverwritten) = await ImportNote(zipEntry, resolveConflict, () => overwriteAll, v => overwriteAll = v);
                    if (wasOverwritten) { overwritten++; }
                    else if (wasImported) { imported++; }
                    else { skipped++; }
                    break;
                }
                case EntryType.Activity:
                {
                    await ImportActivityLog(zipEntry);
                    imported++;
                    break;
                }
            }
        }

        return new ImportSummary { Imported = imported, Skipped = skipped, Overwritten = overwritten };
    }

    private async Task<bool> ImportMessage(ZipArchiveEntry zipEntry)
    {
        MessageExportData? data = await ReadEntry<MessageExportData>(zipEntry);
        if (data is null) { return false; }

        MessageEntity? existing = await messages.Get(data.MessageId, data.IsOutbound);
        if (existing is not null && existing.ReceivedAt.Date == data.ReceivedAt.Date)
        {
            return false;
        }

        object message = engineController.CreateMessage();
        engineController.SetMessageId(message, data.MessageId);
        engineController.SetFromUser(message, data.FromUser);
        engineController.SetSubject(message, data.Subject);
        engineController.SetBody(message, data.Body);
        engineController.SetAddresses(message, data.Addresses
            .Select(a => new MessageAddress { UserName = a.UserName, Type = a.Type.ParseAddressType() })
            .ToList());
        engineController.SetSentAt(message, data.SentAt);
        engineController.SetIsAlert(message, data.IsAlert);
        engineController.SetPriority(message, data.Priority);
        engineController.SetTag(message, data.Tag);

        MessageEntity entity = new()
        {
            MessageId = data.MessageId,
            Message = message,
            DeliveryStatuses = data.DeliveryStatuses,
            ReceivedAt = data.ReceivedAt,
            FolderId = await folders.GetRootId(data.IsOutbound ? FolderType.Outbox : FolderType.Inbox),
            IsOutbound = data.IsOutbound,
            ReadStatus = data.ReadStatus
        };
        await messages.Insert(entity);
        return true;
    }

    private async Task<(bool Imported, bool Overwritten)> ImportDraft(
        ZipArchiveEntry zipEntry,
        Func<ImportConflict, Task<DraftNoteConflictResolution>> resolveConflict,
        Func<bool> getOverwriteAll,
        Action<bool> setOverwriteAll)
    {
        DraftExportData? data = await ReadEntry<DraftExportData>(zipEntry);
        if (data is null) { return (false, false); }

        string subject = data.Subject.Trim();
        DraftEntity? existing = (await drafts.GetAll()).FirstOrDefault(d => d.Subject.Trim() == subject);
        if (existing is null)
        {
            DraftEntity entity = new()
            {
                Subject = data.Subject,
                Body = data.Body,
                Addresses = data.Addresses,
                IsSent = data.IsSent,
                IsAlert = data.IsAlert,
                Priority = data.Priority,
                Tag = data.Tag,
                SentAt = data.SentAt,
                FolderId = await folders.GetRootId(FolderType.Drafts)
            };
            await drafts.Insert(entity);
            return (true, false);
        }

        DraftNoteConflictResolution resolution = getOverwriteAll()
            ? DraftNoteConflictResolution.OverwriteAll
            : await resolveConflict(new ImportConflict { EntryType = EntryType.Draft, Name = subject });

        if (resolution == DraftNoteConflictResolution.KeepExisting)
        {
            return (false, false);
        }

        if (resolution == DraftNoteConflictResolution.OverwriteAll)
        {
            setOverwriteAll(true);
        }

        existing.Subject = data.Subject;
        existing.Body = data.Body;
        existing.Addresses = data.Addresses;
        existing.IsSent = data.IsSent;
        existing.IsAlert = data.IsAlert;
        existing.Priority = data.Priority;
        existing.Tag = data.Tag;
        existing.SentAt = data.SentAt;
        existing.ModifiedAt = DateTime.UtcNow;
        await drafts.Update(existing);
        return (false, true);
    }

    private async Task<(bool Imported, bool Overwritten)> ImportNote(
        ZipArchiveEntry zipEntry,
        Func<ImportConflict, Task<DraftNoteConflictResolution>> resolveConflict,
        Func<bool> getOverwriteAll,
        Action<bool> setOverwriteAll)
    {
        NoteExportData? data = await ReadEntry<NoteExportData>(zipEntry);
        if (data is null) { return (false, false); }

        string firstLine = FirstLine(data.Body);
        NoteEntity? existing = (await notes.GetAll()).FirstOrDefault(n => FirstLine(n.Body) == firstLine);
        if (existing is null)
        {
            NoteEntity entity = new() { Body = data.Body, FolderId = await folders.GetRootId(FolderType.Notes) };
            await notes.Insert(entity);
            return (true, false);
        }

        DraftNoteConflictResolution resolution = getOverwriteAll()
            ? DraftNoteConflictResolution.OverwriteAll
            : await resolveConflict(new ImportConflict { EntryType = EntryType.Note, Name = firstLine });

        if (resolution == DraftNoteConflictResolution.KeepExisting)
        {
            return (false, false);
        }

        if (resolution == DraftNoteConflictResolution.OverwriteAll)
        {
            setOverwriteAll(true);
        }

        existing.Body = data.Body;
        existing.ModifiedAt = DateTime.UtcNow;
        await notes.Update(existing);
        return (false, true);
    }

    private async Task ImportActivityLog(ZipArchiveEntry zipEntry)
    {
        ActivityLogExportData? data = await ReadEntry<ActivityLogExportData>(zipEntry);
        if (data is null) { return; }

        ActivityLogEntity? existing = (await activityLogs.GetAll()).FirstOrDefault(a => a.Date == data.Date);
        if (existing is null)
        {
            ActivityLogEntity entity = new() { Date = data.Date, EventEntries = data.EventEntries };
            await activityLogs.Insert(entity);
            return;
        }

        foreach (ActivityLogEntry entry in data.EventEntries)
        {
            if (existing.EventEntries.Any(e => e.At == entry.At && e.Message == entry.Message))
            {
                continue;
            }

            int insertIndex = existing.EventEntries.FindIndex(e => e.At > entry.At);
            if (insertIndex < 0)
            {
                existing.EventEntries.Add(entry);
            }
            else
            {
                existing.EventEntries.Insert(insertIndex, entry);
            }
        }
        await activityLogs.Update(existing);
    }
}
