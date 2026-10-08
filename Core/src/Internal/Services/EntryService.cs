namespace BlueHeighliner.Comlink;

/// <summary>Provides CRUD operations for messages, drafts, notes, and activity log entries stored in the local database.</summary>
internal interface IEntryService
{
    /// <summary>Raised after an inbound message is persisted to the database.</summary>
    event Func<MessageEntity, Task>? MessageInserted;
    /// <summary>Raised after a new draft is created and persisted.</summary>
    event Func<DraftEntity, Task>? DraftInserted;
    /// <summary>Raised after an existing draft is saved.</summary>
    event Func<DraftEntity, Task>? DraftUpdated;
    /// <summary>Raised after <see cref="SaveDraftQuietly"/> saved a draft, so what shows it can be brought up to date without anything being selected.</summary>
    event Func<DraftEntity, Task>? DraftSavedQuietly;
    /// <summary>Raised after a new note is created and persisted.</summary>
    event Func<NoteEntity, Task>? NoteInserted;
    /// <summary>Raised after an existing note is saved.</summary>
    event Func<NoteEntity, Task>? NoteUpdated;
    /// <summary>Raised after <see cref="SaveNoteQuietly"/> saved a note, so what shows it can be brought up to date without anything being selected.</summary>
    event Func<NoteEntity, Task>? NoteSavedQuietly;
    /// <summary>Raised after an Inbox message's <see cref="MessageEntity.ReadStatus"/> transitions from <c>Received</c> to <c>Read</c>.</summary>
    event Func<MessageEntity, Task>? MessageRead;
    /// <summary>
    /// Persists a message the user sent to the Outbox folder. It lists a delivery status, <see cref="DestinationStatus.Sending"/>, for each addressed user that is not a group and not an external address; the host's
    /// processor reports every destination's outcome, those and the members of the groups it expanded, with <see cref="UpdateDeliveryStatus"/>.
    /// </summary>
    Task<MessageEntity> StoreSentMessage(Message message);
    /// <summary>Returns the stored message with the identifier <paramref name="messageId"/> in the Outbox (<paramref name="outbound"/>) or the Inbox, or <see langword="null"/> when there is none.</summary>
    Task<MessageEntity?> FindMessage(string messageId, bool outbound);
    /// <summary>Updates the delivery status for a specific user on the Outbox record, ignoring a status that would move it backward (for example a late "Sent" after "Confirmed"); user names match case-insensitively.</summary>
    Task<MessageEntity?> UpdateDeliveryStatus(string messageId, string userName, DestinationStatus status);
    /// <summary>Persists a received message to the Inbox folder with <see cref="MessageEntity.ReadStatus"/> set to <see cref="DestinationStatus.Received"/>, and raises <see cref="MessageInserted"/>.</summary>
    Task<MessageEntity> StoreIncomingMessage(Message message);
    /// <summary>Returns whether the Inbox already holds a record for <paramref name="messageId"/>.</summary>
    Task<bool> IncomingMessageExists(string messageId);
    /// <summary>
    /// Transitions the Inbox record for <paramref name="messageId"/> from <see cref="DestinationStatus.Received"/>
    /// to <see cref="DestinationStatus.Read"/> and raises <see cref="MessageRead"/>. Returns <see langword="null"/>
    /// (a no-op) if the record does not exist or is already <see cref="DestinationStatus.Read"/>.
    /// </summary>
    Task<MessageEntity?> MarkMessageRead(string messageId);
    /// <summary>Makes a new draft for the Drafts folder with what the draft handler says a new one starts with, without saving it: nothing exists until <see cref="InsertDraft"/> is called, which is not until it has been written in.</summary>
    Task<DraftEntity> NewDraft();
    /// <summary>Makes a new note for the Notes folder, without saving it: nothing exists until <see cref="InsertNote"/> is called, which is not until it has been written in.</summary>
    Task<NoteEntity> NewNote();
    /// <summary>Saves a draft made with <see cref="NewDraft"/> and raises <see cref="DraftInserted"/>.</summary>
    Task InsertDraft(DraftEntity entity);
    /// <summary>Saves a note made with <see cref="NewNote"/> and raises <see cref="NoteInserted"/>.</summary>
    Task InsertNote(NoteEntity entity);
    /// <summary>Saves a copy of <paramref name="source"/> as a new, unsent draft in the same folder, leaving <paramref name="source"/> as it is, and raises <see cref="DraftInserted"/>.</summary>
    Task<DraftEntity> DuplicateDraft(DraftEntity source);
    /// <summary>Saves a copy of <paramref name="source"/> as a new note in the same folder, leaving <paramref name="source"/> as it is, and raises <see cref="NoteInserted"/>.</summary>
    Task<NoteEntity> DuplicateNote(NoteEntity source);
    /// <summary>Persists changes to an existing draft and raises <see cref="DraftUpdated"/> if it has not yet been sent.</summary>
    Task SaveDraft(DraftEntity entity);
    /// <summary>Persists changes to an existing note and raises <see cref="NoteUpdated"/>.</summary>
    Task SaveNote(NoteEntity entity);
    /// <summary>Persists changes to a draft the user is leaving and raises <see cref="DraftSavedQuietly"/>, which unlike <see cref="DraftUpdated"/> does not bring the draft back to the user's attention by selecting it.</summary>
    Task SaveDraftQuietly(DraftEntity entity);
    /// <summary>Persists changes to a note the user is leaving and raises <see cref="NoteSavedQuietly"/>, which unlike <see cref="NoteUpdated"/> does not bring the note back to the user's attention by selecting it.</summary>
    Task SaveNoteQuietly(NoteEntity entity);
    /// <summary>
    /// Returns a page of messages from the specified folder together with the total message count. A non-empty
    /// <paramref name="filter"/> matches <see cref="EntryFilter.Search"/> case-insensitively against the message's
    /// body, sender, destinations, tag, priority label and message level name; <see cref="EntryFilter.DateFrom"/>/<see cref="EntryFilter.DateTo"/>
    /// bound its received date; <see cref="EntryFilter.Author"/> matches its sender and <see cref="EntryFilter.Destination"/> any addressee (both by substring); <see cref="EntryFilter.MessageLevel"/>/<see cref="EntryFilter.Priority"/> match exactly and <see cref="EntryFilter.Alert"/> keeps only alerts or only non-alerts.
    ///  Filtering loads the whole folder rather than paginating the LiteDB query directly, since a
    /// message's fields live in an embedded document that is not indexed.
    /// </summary>
    Task<(List<MessageEntity> Items, int Total)> GetMessages(string folderId, int page, EntryFilter? filter = null);
    /// <summary>
    /// Returns a page of drafts from the specified folder together with the total draft count. A non-empty
    /// <paramref name="filter"/> matches <see cref="EntryFilter.Search"/> against body or tag;
    /// <see cref="EntryFilter.DateFrom"/>/<see cref="EntryFilter.DateTo"/> bound the last-modified date;
    /// <see cref="EntryFilter.Destination"/> matches any addressee by substring;
    /// <see cref="EntryFilter.MessageLevel"/>/<see cref="EntryFilter.Priority"/> match exactly.
    /// </summary>
    Task<(List<DraftEntity> Items, int Total)> GetDrafts(string folderId, int page, bool alphabetical, EntryFilter? filter = null);
    /// <summary>
    /// Returns a page of notes from the specified folder together with the total note count. A non-empty
    /// <paramref name="filter"/> matches <see cref="EntryFilter.Search"/> against the note's body text, and
    /// <see cref="EntryFilter.DateFrom"/>/<see cref="EntryFilter.DateTo"/> bound the last-modified date; every
    /// other criterion is ignored, since notes have no message level, priority, or alert flag.
    /// </summary>
    Task<(List<NoteEntity> Items, int Total)> GetNotes(string folderId, int page, bool alphabetical, EntryFilter? filter = null);
    /// <summary>
    /// Permanently deletes the entry with the given <paramref name="id"/> and <paramref name="entryType"/>.
    /// <paramref name="isOutboundMessage"/> disambiguates which document to delete when <paramref name="entryType"/>
    /// is <see cref="EntryType.Message"/> and a self-addressed message shares <paramref name="id"/> across an
    /// Inbox and an Outbox record; ignored for other entry types.
    /// </summary>
    Task DeleteEntry(string id, EntryType entryType, bool isOutboundMessage = false);
    /// <summary>
    /// Moves the specified entry to <paramref name="targetFolderId"/>. <paramref name="isOutboundMessage"/>
    /// disambiguates which document to move when <paramref name="entryType"/> is <see cref="EntryType.Message"/>;
    /// see <see cref="DeleteEntry"/>.
    /// </summary>
    Task MoveEntry(string entryId, EntryType entryType, string targetFolderId, bool isOutboundMessage = false);
    /// <summary>Finds the folder that holds the specified entry, or <see langword="null"/> when there is no such entry or it has no folder (an activity log). <paramref name="isOutboundMessage"/> disambiguates which document is meant for a message; see <see cref="DeleteEntry"/>.</summary>
    Task<EntryLocation?> Locate(string id, EntryType entryType, bool isOutboundMessage = false);
    /// <summary>Permanently deletes every message, draft, and note in <paramref name="folderId"/>.</summary>
    Task DeleteFolderContents(string folderId);
    /// <summary>Returns a page of activity log entries together with the total entry count.</summary>
    Task<(List<ActivityLogEntity> Items, int Total)> GetActivityLogs(int page);
}

/// <summary>Provides CRUD operations for messages, drafts, notes, and activity log entries stored in the local database.</summary>
internal sealed class EntryService : IEntryService
{
    private const int PageSize = 50;

    /// <summary>Initializes a new <see cref="EntryService"/> with the required repositories and providers.</summary>
    public EntryService(
        IMessageRepository messages,
        IDraftRepository drafts,
        INoteRepository notes,
        IActivityLogRepository activityLogs,
        IFolderRepository folders,
        ICurrentUserProvider currentUserProvider,
        IEngineController engineController)
    {
        this.messages = messages;
        this.drafts = drafts;
        this.notes = notes;
        this.activityLogs = activityLogs;
        this.folders = folders;
        this.currentUserProvider = currentUserProvider;
        this.engineController = engineController;
    }

    private readonly IMessageRepository messages;
    private readonly IDraftRepository drafts;
    private readonly INoteRepository notes;
    private readonly IActivityLogRepository activityLogs;
    private readonly IFolderRepository folders;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly IEngineController engineController;
    private readonly SemaphoreSlim deliveryLock = new(1, 1);
    private readonly Dictionary<(string MessageId, string UserName), DestinationStatus> pendingStatuses = new();

    /// <summary>Raised after an inbound message is persisted to the database.</summary>
    public event Func<MessageEntity, Task>? MessageInserted;

    /// <summary>Raised after a new draft is created and persisted.</summary>
    public event Func<DraftEntity, Task>? DraftInserted;

    /// <summary>Raised after an existing draft is saved.</summary>
    public event Func<DraftEntity, Task>? DraftUpdated;

    /// <inheritdoc />
    public event Func<DraftEntity, Task>? DraftSavedQuietly;

    /// <summary>Raised after a new note is created and persisted.</summary>
    public event Func<NoteEntity, Task>? NoteInserted;

    /// <summary>Raised after an existing note is saved.</summary>
    public event Func<NoteEntity, Task>? NoteUpdated;

    /// <inheritdoc />
    public event Func<NoteEntity, Task>? NoteSavedQuietly;

    /// <summary>Raised after an Inbox message's <see cref="MessageEntity.ReadStatus"/> transitions from <c>Received</c> to <c>Read</c>.</summary>
    public event Func<MessageEntity, Task>? MessageRead;

    // Status events for one send are raised on separate thread-pool tasks, and can also land after the Outbox record
    // was stored with its final result, so they arrive in any order; a status only ever moves forward, so a late
    // "Sent" can never overwrite "Received". Failed ranks below Received: a later receipt still proves the message arrived.
    private static int DeliveryProgress(DestinationStatus status) => status switch
    {
        DestinationStatus.Sending => 0,
        DestinationStatus.Sent => 1,
        DestinationStatus.Failed => 2,
        DestinationStatus.Received => 3,
        _ => 4
    };

    /// <inheritdoc />
    public async Task<MessageEntity> StoreSentMessage(Message message)
    {
        string outboxId = await folders.GetRootId(FolderType.Outbox);
        await deliveryLock.WaitAsync();
        try
        {
            IReadOnlyDictionary<string, IReadOnlyList<string>> groups = engineController.UserGroups;
            List<DeliveryStatus> deliveryStatuses = [];
            foreach (MessageAddress address in message.Addresses.Where(address => address.Type is not AddressType.External && !groups.ContainsKey(address.UserName)))
            {
                if (deliveryStatuses.Any(existing => string.Equals(existing.UserName, address.UserName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                DestinationStatus status = DestinationStatus.Sending;
                if (pendingStatuses.Remove((message.Id, address.UserName.ToUpperInvariant()), out DestinationStatus early) && DeliveryProgress(early) > DeliveryProgress(status))
                {
                    status = early;
                }

                deliveryStatuses.Add(new DeliveryStatus { UserName = address.UserName, Status = status });
            }

            MessageEntity entity = new()
            {
                MessageId = message.Id,
                Message = engineController.ToData(message),
                DeliveryStatuses = deliveryStatuses,
                ReceivedAt = message.SentAt,
                FolderId = outboxId,
                IsOutbound = true
            };
            await messages.Insert(entity);
            return entity;
        }
        finally
        {
            deliveryLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<MessageEntity?> FindMessage(string messageId, bool outbound) => await messages.Get(messageId, outbound);

    /// <inheritdoc />
    public async Task<MessageEntity?> UpdateDeliveryStatus(string messageId, string userName, DestinationStatus status)
    {
        await deliveryLock.WaitAsync();
        try
        {
            // Delivery status always applies to the Outbox (sent) record. A self-addressed message also has
            // an Inbox (received) record sharing the same MessageId, which must never receive this update.
            MessageEntity? entity = await messages.Get(messageId, outbound: true);
            if (entity is null)
            {
                (string, string) key = (messageId, userName.ToUpperInvariant());
                if (!pendingStatuses.TryGetValue(key, out DestinationStatus pending) || DeliveryProgress(status) > DeliveryProgress(pending))
                {
                    pendingStatuses[key] = status;
                }

                return null;
            }

            DeliveryStatus? existing = entity.DeliveryStatuses.FirstOrDefault(d => string.Equals(d.UserName, userName, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                entity.DeliveryStatuses.Add(new DeliveryStatus { UserName = userName, Status = status });
            }
            else if (DeliveryProgress(status) > DeliveryProgress(existing.Status))
            {
                existing.Status = status;
            }
            else
            {
                return entity;
            }

            await messages.Update(entity);
            return entity;
        }
        finally
        {
            deliveryLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<MessageEntity> StoreIncomingMessage(Message message)
    {
        string inboxId = await folders.GetRootId(FolderType.Inbox);
        MessageEntity entity = new()
        {
            MessageId = message.Id,
            Message = engineController.ToData(message),
            ReceivedAt = message.SentAt,
            FolderId = inboxId,
            ReadStatus = DestinationStatus.Received
        };

        await messages.Insert(entity);

        await MessageInserted.InvokeAll(entity);

        return entity;
    }

    /// <inheritdoc />
    public async Task<bool> IncomingMessageExists(string messageId) => await messages.Get(messageId, outbound: false) is not null;

    /// <summary>
    /// Transitions the Inbox record for <paramref name="messageId"/> from <see cref="DestinationStatus.Received"/>
    /// to <see cref="DestinationStatus.Read"/> and raises <see cref="MessageRead"/>. Returns <see langword="null"/>
    /// (a no-op) if the record does not exist or is already <see cref="DestinationStatus.Read"/>.
    /// </summary>
    public async Task<MessageEntity?> MarkMessageRead(string messageId)
    {
        MessageEntity? entity = await messages.Get(messageId, outbound: false);
        if (entity is null || entity.ReadStatus is not DestinationStatus.Received)
        {
            return null;
        }

        entity.ReadStatus = DestinationStatus.Read;
        await messages.Update(entity);

        await MessageRead.InvokeAll(entity);

        return entity;
    }

    /// <inheritdoc />
    public async Task<DraftEntity> NewDraft()
    {
        string draftsId = await folders.GetRootId(FolderType.Drafts);
        DraftDefaults defaults = engineController.DraftDefaults;
        return new DraftEntity
        {
            FolderId = draftsId,
            Tag = defaults.Tag,
            Priority = defaults.Priority is { } priority ? engineController.StoredPriority(priority) : 0,
            MessageLevel = defaults.MessageLevel is { } level ? engineController.MessageLevels.FirstOrDefault(candidate => level.Equals(candidate.Key))?.Value : null,
            MessageAspect = defaults.MessageAspect is { } aspect ? engineController.MessageAspects.FirstOrDefault(candidate => aspect.Equals(candidate.Key))?.Value : null
        };
    }

    /// <inheritdoc />
    public async Task<NoteEntity> NewNote() => new() { FolderId = await folders.GetRootId(FolderType.Notes) };

    /// <inheritdoc />
    public async Task InsertDraft(DraftEntity entity)
    {
        entity.ModifiedAt = DateTime.UtcNow;
        await drafts.Insert(entity);
        await DraftInserted.InvokeAll(entity);
    }

    /// <inheritdoc />
    public async Task InsertNote(NoteEntity entity)
    {
        entity.ModifiedAt = DateTime.UtcNow;
        await notes.Insert(entity);
        await NoteInserted.InvokeAll(entity);
    }

    /// <inheritdoc />
    public async Task<DraftEntity> DuplicateDraft(DraftEntity source)
    {
        DraftEntity copy = new()
        {
            Name = source.Name,
            Body = source.Body,
            BodySegmentsJson = source.BodySegmentsJson,
            Addresses = [.. source.Addresses.Select(a => new AddressData { UserName = a.UserName, Type = a.Type, Information = a.Information })],
            IsAlert = source.IsAlert,
            Priority = source.Priority,
            Tag = source.Tag,
            MessageLevel = source.MessageLevel,
            MessageAspect = source.MessageAspect,
            LineWidth = source.LineWidth,
            FolderId = source.FolderId
        };
        await InsertDraft(copy);
        return copy;
    }

    /// <inheritdoc />
    public async Task<NoteEntity> DuplicateNote(NoteEntity source)
    {
        NoteEntity copy = new() { Name = source.Name, Body = source.Body, FolderId = source.FolderId };
        await InsertNote(copy);
        return copy;
    }

    /// <summary>Persists changes to an existing draft and raises <see cref="DraftUpdated"/> if it has not yet been sent.</summary>
    public async Task SaveDraft(DraftEntity entity)
    {
        entity.ModifiedAt = DateTime.UtcNow;
        await drafts.Update(entity);
        if (!entity.IsSent)
        {
            await DraftUpdated.InvokeAll(entity);
        }
    }

    /// <inheritdoc />
    public async Task SaveDraftQuietly(DraftEntity entity)
    {
        entity.ModifiedAt = DateTime.UtcNow;
        await drafts.Update(entity);
        await DraftSavedQuietly.InvokeAll(entity);
    }

    /// <inheritdoc />
    public async Task SaveNoteQuietly(NoteEntity entity)
    {
        entity.ModifiedAt = DateTime.UtcNow;
        await notes.Update(entity);
        await NoteSavedQuietly.InvokeAll(entity);
    }

    /// <summary>Persists changes to an existing note and raises <see cref="NoteUpdated"/>.</summary>
    public async Task SaveNote(NoteEntity entity)
    {
        entity.ModifiedAt = DateTime.UtcNow;
        await notes.Update(entity);
        await NoteUpdated.InvokeAll(entity);
    }

    /// <inheritdoc />
    public async Task<(List<MessageEntity> Items, int Total)> GetMessages(string folderId, int page, EntryFilter? filter = null)
    {
        if (filter is null || filter.IsEmpty)
        {
            List<MessageEntity> items = await messages.GetPage(folderId, page);
            int total = await messages.Count(folderId);
            return (items, total);
        }

        List<MessageEntity> matched = [.. (await messages.GetAllInFolder(folderId)).Where(m => MatchesMessage(m, filter))];
        return Paginate(matched, page);
    }

    /// <inheritdoc />
    public async Task<(List<DraftEntity> Items, int Total)> GetDrafts(string folderId, int page, bool alphabetical, EntryFilter? filter = null)
    {
        if (filter is null || filter.IsEmpty)
        {
            List<DraftEntity> items = await drafts.GetPage(folderId, page, alphabetical);
            int total = await drafts.Count(folderId);
            return (items, total);
        }

        List<DraftEntity> matched = [.. (await drafts.GetAllInFolder(folderId, alphabetical)).Where(d => MatchesDraft(d, filter))];
        return Paginate(matched, page);
    }

    /// <inheritdoc />
    public async Task<(List<NoteEntity> Items, int Total)> GetNotes(string folderId, int page, bool alphabetical, EntryFilter? filter = null)
    {
        if (filter is null || filter.IsEmpty)
        {
            List<NoteEntity> items = await notes.GetPage(folderId, page, alphabetical);
            int total = await notes.Count(folderId);
            return (items, total);
        }

        List<NoteEntity> matched = [.. (await notes.GetAllInFolder(folderId, alphabetical)).Where(n => MatchesNote(n, filter))];
        return Paginate(matched, page);
    }

    private bool MatchesMessage(MessageEntity entity, EntryFilter filter)
    {
        MessageData message = entity.Message;
        string levelName = engineController.NameOfLevel(message);
        if (filter.DateFrom is { } from && entity.ReceivedAt < from)
        {
            return false;
        }
        if (filter.DateTo is { } to && entity.ReceivedAt > to)
        {
            return false;
        }
        if (filter.Priority is { } priority && !engineController.PriorityOf(message.Priority).Equals(priority))
        {
            return false;
        }
        if (filter.Alert is { } alert && message.IsAlert != alert)
        {
            return false;
        }
        if (filter.MessageLevel is { } level && !string.Equals(levelName, level, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(filter.Author) && !Contains(message.FromUser, filter.Author.Trim()))
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(filter.Destination) && !message.Addresses.Any(a => Contains(a.UserName, filter.Destination.Trim())))
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(filter.Search))
        {
            return true;
        }
        string search = filter.Search;

        string destinations = string.Join(" ", message.Addresses.Select(a => a.UserName));
        string priorityLabel = engineController.NameOf(engineController.PriorityOf(message.Priority));
        return Contains(message.Body, search)
            || Contains(message.FromUser, search)
            || Contains(destinations, search)
            || Contains(message.Tag, search)
            || Contains(priorityLabel, search)
            || Contains(levelName, search);
    }

    private bool MatchesDraft(DraftEntity entity, EntryFilter filter)
    {
        if (filter.DateFrom is { } from && entity.ModifiedAt < from)
        {
            return false;
        }
        if (filter.DateTo is { } to && entity.ModifiedAt > to)
        {
            return false;
        }
        if (filter.Priority is { } priority && !engineController.PriorityOf(entity.Priority).Equals(priority))
        {
            return false;
        }
        if (filter.MessageLevel is { } level && !string.Equals(engineController.MessageLevels.FirstOrDefault(candidate => candidate.Value == entity.MessageLevel)?.Name, level, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(filter.Destination) && !entity.Addresses.Any(a => Contains(a.UserName, filter.Destination.Trim())))
        {
            return false;
        }
        return string.IsNullOrWhiteSpace(filter.Search) || Contains(entity.Body, filter.Search) || Contains(entity.Tag, filter.Search) || Contains(entity.Name ?? string.Empty, filter.Search);
    }

    private static bool MatchesNote(NoteEntity entity, EntryFilter filter)
    {
        if (filter.DateFrom is { } from && entity.ModifiedAt < from)
        {
            return false;
        }
        if (filter.DateTo is { } to && entity.ModifiedAt > to)
        {
            return false;
        }
        return string.IsNullOrWhiteSpace(filter.Search) || Contains(entity.Body, filter.Search) || Contains(entity.Name ?? string.Empty, filter.Search);
    }

    private static bool Contains(string? value, string search) => !string.IsNullOrEmpty(value) && value.Contains(search, StringComparison.OrdinalIgnoreCase);

    private static (List<T> Items, int Total) Paginate<T>(List<T> matched, int page)
        => ([.. matched.Skip((page - 1) * PageSize).Take(PageSize)], matched.Count);

    /// <inheritdoc />
    public async Task DeleteEntry(string id, EntryType entryType, bool isOutboundMessage = false)
    {
        switch (entryType)
        {
            case EntryType.Message:
                await messages.Delete(id, isOutboundMessage);
                break;
            case EntryType.Draft:
                try { await drafts.Delete(new ObjectId(id)); } catch { }
                break;
            case EntryType.Note:
                try { await notes.Delete(new ObjectId(id)); } catch { }
                break;
        }
    }

    /// <inheritdoc />
    public async Task DeleteFolderContents(string folderId)
    {
        await messages.DeleteAll(folderId);
        await drafts.DeleteAll(folderId);
        await notes.DeleteAll(folderId);
    }

    /// <inheritdoc />
    public async Task<EntryLocation?> Locate(string id, EntryType entryType, bool isOutboundMessage = false)
    {
        switch (entryType)
        {
            case EntryType.Message:
                return await messages.Get(id, isOutboundMessage) is { } message ? new EntryLocation(message.FolderId, message.Message.IsAlert) : null;
            case EntryType.Draft:
                return await drafts.Get(new ObjectId(id)) is { } draft ? new EntryLocation(draft.FolderId, false) : null;
            case EntryType.Note:
                return await notes.Get(new ObjectId(id)) is { } note ? new EntryLocation(note.FolderId, false) : null;
            default:
                return null;
        }
    }

    /// <inheritdoc />
    public async Task MoveEntry(string entryId, EntryType entryType, string targetFolderId, bool isOutboundMessage = false)
    {
        switch (entryType)
        {
            case EntryType.Message:
                MessageEntity? msg = await messages.Get(entryId, isOutboundMessage);
                if (msg is not null)
                {
                    msg.FolderId = targetFolderId;
                    await messages.Update(msg);
                }
                break;
            case EntryType.Draft:
                DraftEntity? draft = await drafts.Get(new ObjectId(entryId));
                if (draft is not null)
                {
                    draft.FolderId = targetFolderId;
                    await drafts.Update(draft);
                }
                break;
            case EntryType.Note:
                NoteEntity? note = await notes.Get(new ObjectId(entryId));
                if (note is not null)
                {
                    note.FolderId = targetFolderId;
                    await notes.Update(note);
                }
                break;
        }
    }

    /// <summary>Returns a page of activity log entries together with the total entry count.</summary>
    public async Task<(List<ActivityLogEntity> Items, int Total)> GetActivityLogs(int page)
    {
        List<ActivityLogEntity> items = await activityLogs.GetPage(page);
        int total = await activityLogs.Count();
        return (items, total);
    }
}
