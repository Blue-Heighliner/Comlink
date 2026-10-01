namespace BlueHeighliner.Comlink.Services;

/// <summary>Provides CRUD operations for messages, drafts, notes, and activity log entries stored in the local database.</summary>
internal interface IEntryService
{
    /// <summary>Raised after an inbound message is persisted to the database.</summary>
    event Func<MessageEntity, Task>? MessageInserted;
    /// <summary>Raised after a new draft is created and persisted.</summary>
    event Func<DraftEntity, Task>? DraftInserted;
    /// <summary>Raised after an existing draft is saved.</summary>
    event Func<DraftEntity, Task>? DraftUpdated;
    /// <summary>Raised after a new note is created and persisted.</summary>
    event Func<NoteEntity, Task>? NoteInserted;
    /// <summary>Raised after an existing note is saved.</summary>
    event Func<NoteEntity, Task>? NoteUpdated;
    /// <summary>Raised after an Inbox message's <see cref="MessageEntity.ReadStatus"/> transitions from <c>Received</c> to <c>Read</c>.</summary>
    event Func<MessageEntity, Task>? MessageRead;
    /// <summary>Persists a sent message to the Outbox folder, including per-user delivery status entries.</summary>
    Task<MessageEntity> StoreSentMessage(string messageId, string subject, string body, List<AddressData> addresses, DateTime sentAt, IReadOnlyList<UserDeliveryResult> userResults, bool isAlert = false, int priority = 0, string tag = "", string securityLevel = "");
    /// <summary>Updates the delivery status for a specific user on the Outbox record, ignoring a status that would move it backward (for example a late "Sent" after "Confirmed"); user names match case-insensitively.</summary>
    Task<MessageEntity?> UpdateDeliveryStatus(string messageId, string userName, DestinationStatus status);
    /// <summary>Persists a received message to the Inbox folder with <see cref="MessageEntity.ReadStatus"/> set to <see cref="DestinationStatus.Received"/>, and raises <see cref="MessageInserted"/>.</summary>
    Task<MessageEntity> StoreIncomingMessage(string messageId, string fromUser, string subject, string body, List<AddressData> addresses, DateTime sentAt, bool isAlert = false, int priority = 0, string tag = "", string securityLevel = "");
    /// <summary>Returns whether the Inbox already holds a record for <paramref name="messageId"/>.</summary>
    Task<bool> IncomingMessageExists(string messageId);
    /// <summary>
    /// Transitions the Inbox record for <paramref name="messageId"/> from <see cref="DestinationStatus.Received"/>
    /// to <see cref="DestinationStatus.Read"/> and raises <see cref="MessageRead"/>. Returns <see langword="null"/>
    /// (a no-op) if the record does not exist or is already <see cref="DestinationStatus.Read"/>.
    /// </summary>
    Task<MessageEntity?> MarkMessageRead(string messageId);
    /// <summary>Creates a new empty draft in the Drafts folder and raises <see cref="DraftInserted"/>.</summary>
    Task<DraftEntity> CreateDraft();
    /// <summary>Creates a new empty note in the Notes folder and raises <see cref="NoteInserted"/>.</summary>
    Task<NoteEntity> CreateNote();
    /// <summary>Persists changes to an existing draft and raises <see cref="DraftUpdated"/> if it has not yet been sent.</summary>
    Task SaveDraft(DraftEntity entity);
    /// <summary>Persists changes to an existing note and raises <see cref="NoteUpdated"/>.</summary>
    Task SaveNote(NoteEntity entity);
    /// <summary>
    /// Returns a page of messages from the specified folder together with the total message count. A non-empty
    /// <paramref name="filter"/> matches <see cref="EntryFilter.Search"/> case-insensitively against the message's
    /// subject, sender, destinations, tag, priority label and security level name; <see cref="EntryFilter.DateFrom"/>/<see cref="EntryFilter.DateTo"/>
    /// bound its received date; <see cref="EntryFilter.Author"/> matches its sender and <see cref="EntryFilter.Destination"/> any addressee (both by substring); <see cref="EntryFilter.SecurityLevel"/>/<see cref="EntryFilter.Priority"/>/<see cref="EntryFilter.AlertOnly"/>
    /// match exactly. Filtering loads the whole folder rather than paginating the LiteDB query directly, since a
    /// message's fields live inside the host's own opaque frame type and cannot be queried in the database.
    /// </summary>
    Task<(List<MessageEntity> Items, int Total)> GetMessages(string folderId, int page, EntryFilter? filter = null);
    /// <summary>
    /// Returns a page of drafts from the specified folder together with the total draft count. A non-empty
    /// <paramref name="filter"/> matches <see cref="EntryFilter.Search"/> against subject or tag;
    /// <see cref="EntryFilter.DateFrom"/>/<see cref="EntryFilter.DateTo"/> bound the last-modified date;
    /// <see cref="EntryFilter.Destination"/> matches any addressee by substring;
    /// <see cref="EntryFilter.SecurityLevel"/>/<see cref="EntryFilter.Priority"/>/<see cref="EntryFilter.AlertOnly"/> match exactly.
    /// </summary>
    Task<(List<DraftEntity> Items, int Total)> GetDrafts(string folderId, int page, bool alphabetical, EntryFilter? filter = null);
    /// <summary>
    /// Returns a page of notes from the specified folder together with the total note count. A non-empty
    /// <paramref name="filter"/> matches <see cref="EntryFilter.Search"/> against the note's body text, and
    /// <see cref="EntryFilter.DateFrom"/>/<see cref="EntryFilter.DateTo"/> bound the last-modified date; every
    /// other criterion is ignored, since notes have no security level, priority, or alert flag.
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

    /// <summary>Raised after a new note is created and persisted.</summary>
    public event Func<NoteEntity, Task>? NoteInserted;

    /// <summary>Raised after an existing note is saved.</summary>
    public event Func<NoteEntity, Task>? NoteUpdated;

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

    private object BuildMessage(string messageId, string fromUser, string subject, string body, List<AddressData> addresses, DateTime sentAt, bool isAlert, int priority, string tag, string securityLevel)
    {
        object message = engineController.CreateMessage(new MessageCreateContext
        {
            Subject = subject,
            Body = body,
            IsAlert = isAlert,
            Priority = priority,
            Tag = tag,
            SecurityLevel = securityLevel
        });
        engineController.SetFrameId(message, messageId);
        engineController.SetFromUser(message, fromUser);
        engineController.SetAddresses(message, addresses.Select(a => new MessageAddress { UserName = a.UserName, Type = a.Type.ParseAddressType(), Information = a.Information }).ToList());
        engineController.SetSentAt(message, sentAt);
        return message;
    }

    /// <summary>Persists a sent message to the Outbox folder, including per-user delivery status entries.</summary>
    public async Task<MessageEntity> StoreSentMessage(string messageId, string subject, string body, List<AddressData> addresses, DateTime sentAt, IReadOnlyList<UserDeliveryResult> userResults, bool isAlert = false, int priority = 0, string tag = "", string securityLevel = "")
    {
        string outboxId = await folders.GetRootId(FolderType.Outbox);
        await deliveryLock.WaitAsync();
        try
        {
            return await InsertSentMessage(messageId, subject, body, addresses, sentAt, userResults, isAlert, priority, tag, securityLevel, outboxId);
        }
        finally
        {
            deliveryLock.Release();
        }
    }

    private async Task<MessageEntity> InsertSentMessage(string messageId, string subject, string body, List<AddressData> addresses, DateTime sentAt, IReadOnlyList<UserDeliveryResult> userResults, bool isAlert, int priority, string tag, string securityLevel, string outboxId)
    {
        List<DeliveryStatus> deliveryStatuses = [];
        foreach (UserDeliveryResult result in userResults)
        {
            DestinationStatus status = result.Success ? DestinationStatus.Sent : DestinationStatus.Failed;
            if (pendingStatuses.Remove((messageId, result.UserName.ToUpperInvariant()), out DestinationStatus early) && DeliveryProgress(early) > DeliveryProgress(status))
            {
                status = early;
            }

            deliveryStatuses.Add(new DeliveryStatus { UserName = result.UserName, Status = status, AddressedVia = [.. result.AddressedVia] });
        }

        MessageEntity entity = new()
        {
            MessageId = messageId,
            Message = BuildMessage(messageId, currentUserProvider.UserName ?? string.Empty, subject, body, addresses, sentAt, isAlert, priority, tag, securityLevel),
            DeliveryStatuses = deliveryStatuses,
            ReceivedAt = sentAt,
            FolderId = outboxId,
            IsOutbound = true
        };
        await messages.Insert(entity);
        return entity;
    }

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

    /// <summary>Persists a received message to the Inbox folder and raises <see cref="MessageInserted"/>.</summary>
    public async Task<MessageEntity> StoreIncomingMessage(string messageId, string fromUser, string subject, string body, List<AddressData> addresses, DateTime sentAt, bool isAlert = false, int priority = 0, string tag = "", string securityLevel = "")
    {
        string inboxId = await folders.GetRootId(FolderType.Inbox);
        MessageEntity entity = new()
        {
            MessageId = messageId,
            Message = BuildMessage(messageId, fromUser, subject, body, addresses, sentAt, isAlert, priority, tag, securityLevel),
            ReceivedAt = sentAt,
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
        if (entity is null || entity.ReadStatus != DestinationStatus.Received) { return null; }

        entity.ReadStatus = DestinationStatus.Read;
        await messages.Update(entity);

        await MessageRead.InvokeAll(entity);

        return entity;
    }

    /// <summary>Creates a new empty draft in the Drafts folder and raises <see cref="DraftInserted"/>.</summary>
    public async Task<DraftEntity> CreateDraft()
    {
        string draftsId = await folders.GetRootId(FolderType.Drafts);
        DraftEntity entity = new() { FolderId = draftsId };
        await drafts.Insert(entity);

        await DraftInserted.InvokeAll(entity);

        return entity;
    }

    /// <summary>Creates a new empty note in the Notes folder and raises <see cref="NoteInserted"/>.</summary>
    public async Task<NoteEntity> CreateNote()
    {
        string notesId = await folders.GetRootId(FolderType.Notes);
        NoteEntity entity = new() { FolderId = notesId };
        await notes.Insert(entity);

        await NoteInserted.InvokeAll(entity);

        return entity;
    }

    /// <summary>Persists changes to an existing draft and raises <see cref="DraftUpdated"/> if it has not yet been sent.</summary>
    public async Task SaveDraft(DraftEntity entity)
    {
        entity.ModifiedAt = DateTime.UtcNow;
        await drafts.Update(entity);
        if (!entity.IsSent) { await DraftUpdated.InvokeAll(entity); }
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
        object message = entity.Message;
        if (filter.DateFrom is { } from && entity.ReceivedAt < from) { return false; }
        if (filter.DateTo is { } to && entity.ReceivedAt > to) { return false; }
        if (filter.Priority is { } priority && engineController.GetPriority(message) != priority) { return false; }
        if (filter.AlertOnly is true && !engineController.GetIsAlert(message)) { return false; }
        if (filter.SecurityLevel is { } level && !string.Equals(engineController.GetSecurityLevel(message), level, StringComparison.OrdinalIgnoreCase)) { return false; }
        if (!string.IsNullOrWhiteSpace(filter.Author) && !Contains(engineController.GetFromUser(message), filter.Author.Trim())) { return false; }
        if (!string.IsNullOrWhiteSpace(filter.Destination) && !engineController.GetAddresses(message).Any(a => Contains(a.UserName, filter.Destination.Trim()))) { return false; }
        if (string.IsNullOrWhiteSpace(filter.Search)) { return true; }
        string search = filter.Search;

        string destinations = string.Join(" ", engineController.GetAddresses(message).Select(a => a.UserName));
        string priorityLabel = engineController.Priorities.GetLabel(engineController.GetPriority(message));
        return Contains(engineController.GetSubject(message), search)
            || Contains(engineController.GetFromUser(message), search)
            || Contains(destinations, search)
            || Contains(engineController.GetTag(message), search)
            || Contains(priorityLabel, search)
            || Contains(engineController.GetSecurityLevel(message), search);
    }

    private static bool MatchesDraft(DraftEntity entity, EntryFilter filter)
    {
        if (filter.DateFrom is { } from && entity.ModifiedAt < from) { return false; }
        if (filter.DateTo is { } to && entity.ModifiedAt > to) { return false; }
        if (filter.Priority is { } priority && entity.Priority != priority) { return false; }
        if (filter.AlertOnly is true && !entity.IsAlert) { return false; }
        if (filter.SecurityLevel is { } level && !string.Equals(entity.SecurityLevel, level, StringComparison.OrdinalIgnoreCase)) { return false; }
        if (!string.IsNullOrWhiteSpace(filter.Destination) && !entity.Addresses.Any(a => Contains(a.UserName, filter.Destination.Trim()))) { return false; }
        return string.IsNullOrWhiteSpace(filter.Search) || Contains(entity.Subject, filter.Search) || Contains(entity.Tag, filter.Search);
    }

    private static bool MatchesNote(NoteEntity entity, EntryFilter filter)
    {
        if (filter.DateFrom is { } from && entity.ModifiedAt < from) { return false; }
        if (filter.DateTo is { } to && entity.ModifiedAt > to) { return false; }
        return string.IsNullOrWhiteSpace(filter.Search) || Contains(entity.Body, filter.Search);
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
    public async Task MoveEntry(string entryId, EntryType entryType, string targetFolderId, bool isOutboundMessage = false)
    {
        switch (entryType)
        {
            case EntryType.Message:
                MessageEntity? msg = await messages.Get(entryId, isOutboundMessage);
                if (msg is not null) { msg.FolderId = targetFolderId; await messages.Update(msg); }
                break;
            case EntryType.Draft:
                DraftEntity? draft = await drafts.Get(new ObjectId(entryId));
                if (draft is not null) { draft.FolderId = targetFolderId; await drafts.Update(draft); }
                break;
            case EntryType.Note:
                NoteEntity? note = await notes.Get(new ObjectId(entryId));
                if (note is not null) { note.FolderId = targetFolderId; await notes.Update(note); }
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
