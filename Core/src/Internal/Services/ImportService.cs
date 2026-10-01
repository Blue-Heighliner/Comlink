namespace BlueHeighliner.Comlink.Services;

/// <summary>Lists export packages on a drive and restores their entries into the local database.</summary>
internal interface IImportService
{
    /// <summary>
    /// Returns every file directly under <paramref name="driveRootPath"/> available to import as an
    /// <see cref="ImportPackageInfo"/>, ordered by file name: every <see cref="IExportService.PackageExtension"/>
    /// package when <paramref name="format"/> is <see langword="null"/>, or every file whose extension matches
    /// <paramref name="format"/>'s own <see cref="ImportFormatDefinition.FileExtension"/> otherwise.
    /// </summary>
    IReadOnlyList<ImportPackageInfo> GetPackages(string driveRootPath, ImportFormatDefinition? format = null);

    /// <summary>
    /// Restores the file at <paramref name="packagePath"/> into the local database. When <paramref name="format"/>
    /// is <see langword="null"/>, restores every entry in the built-in package:
    /// <list type="bullet">
    /// <item>A message matching an existing message's ID, direction, and date is skipped.</item>
    /// <item>
    /// A draft/note matching an existing entry's name (first line of the body) invokes
    /// <paramref name="resolveConflict"/> to ask how to proceed, unless a prior conflict in this same call
    /// was resolved as <see cref="DraftNoteConflictResolution.OverwriteAll"/>, in which case it is
    /// overwritten without asking.
    /// </item>
    /// <item>An activity log matching an existing log's date is merged into it line by line, skipping any imported line that exactly matches an existing one and inserting the rest in timestamp order.</item>
    /// </list>
    /// Otherwise, opens the file as a stream and calls <paramref name="format"/>'s own reader, handing it an
    /// <see cref="IImportFormatContext"/> that applies the same message/draft/note rules above to whatever the
    /// reader adds through it, and collects whatever staged sends it adds into the returned summary.
    /// </summary>
    /// <param name="packagePath">Absolute path of the file to import.</param>
    /// <param name="resolveConflict">Invoked once per unresolved draft/note name conflict to obtain the user's choice.</param>
    /// <param name="format">The custom format to read with, or <see langword="null"/> for the built-in package format.</param>
    /// <param name="cancellation">Token used to cancel a custom format's read mid-way.</param>
    Task<ImportSummary> Import(string packagePath, Func<ImportConflict, Task<DraftNoteConflictResolution>> resolveConflict, ImportFormatDefinition? format = null, CancellationToken cancellation = default);
}

/// <summary>Lists export packages on a drive and restores their entries into the local database.</summary>
internal sealed class ImportService : IImportService
{
    private static List<AddressData> ToAddressData(List<AddressRequest> addresses)
        => [.. addresses.Select(a => new AddressData { UserName = a.UserName, Type = a.Type, Information = a.Information })];

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
    public IReadOnlyList<ImportPackageInfo> GetPackages(string driveRootPath, ImportFormatDefinition? format = null)
    {
        try
        {
            string extension = format is null ? IExportService.PackageExtension : $".{format.FileExtension}";
            return Directory.GetFiles(driveRootPath, $"*{extension}")
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
    public async Task<ImportSummary> Import(string packagePath, Func<ImportConflict, Task<DraftNoteConflictResolution>> resolveConflict, ImportFormatDefinition? format = null, CancellationToken cancellation = default)
    {
        if (format is not null)
        {
            await using Stream stream = File.OpenRead(packagePath);
            ImportFormatContext context = new(ApplyMessage, ApplyDraft, ApplyNote, resolveConflict);
            await format.Read(stream, context, cancellation);
            return context.BuildSummary();
        }

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
                        MessageExportData? data = await ReadEntry<MessageExportData>(zipEntry);
                        if (data is not null && await ApplyMessage(data)) { imported++; }
                        else { skipped++; }
                        break;
                    }
                case EntryType.Draft:
                    {
                        DraftExportData? data = await ReadEntry<DraftExportData>(zipEntry);
                        if (data is null) { skipped++; break; }
                        (bool wasImported, bool wasOverwritten) = await ApplyDraft(data, resolveConflict, () => overwriteAll, v => overwriteAll = v);
                        if (wasOverwritten) { overwritten++; }
                        else if (wasImported) { imported++; }
                        else { skipped++; }
                        break;
                    }
                case EntryType.Note:
                    {
                        NoteExportData? data = await ReadEntry<NoteExportData>(zipEntry);
                        if (data is null) { skipped++; break; }
                        (bool wasImported, bool wasOverwritten) = await ApplyNote(data, resolveConflict, () => overwriteAll, v => overwriteAll = v);
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

    private async Task<bool> ApplyMessage(MessageExportData data)
    {
        MessageEntity? existing = await messages.Get(data.MessageId, data.IsOutbound);
        if (existing is not null && existing.ReceivedAt.Date == data.ReceivedAt.Date)
        {
            return false;
        }

        object message = engineController.CreateMessage(new MessageCreateContext
        {
            Body = data.Body,
            IsAlert = data.IsAlert,
            Priority = data.Priority,
            Tag = data.Tag,
            SecurityLevel = string.Empty
        });
        engineController.SetFrameId(message, data.MessageId);
        engineController.SetFromUser(message, data.FromUser);
        engineController.SetAddresses(message, data.Addresses
            .Select(a => new MessageAddress { UserName = a.UserName, Type = a.Type.ParseAddressType(), Information = a.Information })
            .ToList());
        engineController.SetSentAt(message, data.SentAt);

        MessageEntity entity = new()
        {
            MessageId = data.MessageId,
            Message = message,
            DeliveryStatuses = [.. data.DeliveryStatuses.Select(d => new DeliveryStatus { UserName = d.UserName, Status = d.Status, AddressedVia = d.AddressedVia })],
            ReceivedAt = data.ReceivedAt,
            FolderId = await folders.GetRootId(data.IsOutbound ? FolderType.Outbox : FolderType.Inbox),
            IsOutbound = data.IsOutbound,
            ReadStatus = data.ReadStatus
        };
        await messages.Insert(entity);
        return true;
    }

    private async Task<(bool Imported, bool Overwritten)> ApplyDraft(
        DraftExportData data,
        Func<ImportConflict, Task<DraftNoteConflictResolution>> resolveConflict,
        Func<bool> getOverwriteAll,
        Action<bool> setOverwriteAll)
    {
        string name = data.Body.FirstLine;
        DraftEntity? existing = (await drafts.GetAll()).FirstOrDefault(d => d.Body.FirstLine == name);
        if (existing is null)
        {
            DraftEntity entity = new()
            {
                Body = data.Body,
                BodySegmentsJson = data.BodySegmentsJson ?? string.Empty,
                Addresses = ToAddressData(data.Addresses),
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
            : await resolveConflict(new ImportConflict { EntryType = EntryType.Draft, Name = name });

        if (resolution == DraftNoteConflictResolution.KeepExisting)
        {
            return (false, false);
        }

        if (resolution == DraftNoteConflictResolution.OverwriteAll)
        {
            setOverwriteAll(true);
        }

        existing.Body = data.Body;
        existing.BodySegmentsJson = data.BodySegmentsJson ?? string.Empty;
        existing.Addresses = ToAddressData(data.Addresses);
        existing.IsSent = data.IsSent;
        existing.IsAlert = data.IsAlert;
        existing.Priority = data.Priority;
        existing.Tag = data.Tag;
        existing.SentAt = data.SentAt;
        existing.ModifiedAt = DateTime.UtcNow;
        await drafts.Update(existing);
        return (false, true);
    }

    private async Task<(bool Imported, bool Overwritten)> ApplyNote(
        NoteExportData data,
        Func<ImportConflict, Task<DraftNoteConflictResolution>> resolveConflict,
        Func<bool> getOverwriteAll,
        Action<bool> setOverwriteAll)
    {
        string firstLine = data.Body.FirstLine;
        NoteEntity? existing = (await notes.GetAll()).FirstOrDefault(n => n.Body.FirstLine == firstLine);
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
            ActivityLogEntity entity = new() { Date = data.Date, EventEntries = [.. data.EventEntries.Select(e => new ActivityLogEntry { At = e.At, Message = e.Message })] };
            await activityLogs.Insert(entity);
            return;
        }

        foreach (ActivityLogEventEntry entry in data.EventEntries)
        {
            if (existing.EventEntries.Any(e => e.At == entry.At && e.Message == entry.Message))
            {
                continue;
            }

            ActivityLogEntry converted = new() { At = entry.At, Message = entry.Message };
            int insertIndex = existing.EventEntries.FindIndex(e => e.At > entry.At);
            if (insertIndex < 0)
            {
                existing.EventEntries.Add(converted);
            }
            else
            {
                existing.EventEntries.Insert(insertIndex, converted);
            }
        }
        await activityLogs.Update(existing);
    }
}
