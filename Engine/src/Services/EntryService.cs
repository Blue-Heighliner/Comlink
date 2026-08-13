namespace BlueHeighliner.Comlink.Engine.Services;

/// <summary>Provides CRUD operations for messages, drafts, notes, and activity log entries stored in the local database.</summary>
public interface IEntryService
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
    Task<MessageEntity> StoreSentMessage(string messageId, string subject, string body, List<AddressData> addresses, DateTime sentAt, IReadOnlyList<UserDeliveryResult> userResults, bool isAlert = false, int priority = 0, string tag = "");
    /// <summary>Updates the delivery status for a specific user on an existing message entity.</summary>
    Task<MessageEntity?> UpdateDeliveryStatus(string messageId, string userName, DestinationStatus status);
    /// <summary>Persists a received message to the Inbox folder with <see cref="MessageEntity.ReadStatus"/> set to <see cref="DestinationStatus.Received"/>, and raises <see cref="MessageInserted"/>.</summary>
    Task<MessageEntity> StoreIncomingMessage(string messageId, string fromUser, string subject, string body, List<AddressData> addresses, DateTime sentAt, bool isAlert = false, int priority = 0, string tag = "");
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
    /// <summary>Returns a page of messages from the specified folder together with the total message count.</summary>
    Task<(List<MessageEntity> Items, int Total)> GetMessages(string folderId, int page);
    /// <summary>Returns a page of drafts from the specified folder together with the total draft count.</summary>
    Task<(List<DraftEntity> Items, int Total)> GetDrafts(string folderId, int page, bool alphabetical);
    /// <summary>Returns a page of notes from the specified folder together with the total note count.</summary>
    Task<(List<NoteEntity> Items, int Total)> GetNotes(string folderId, int page, bool alphabetical);
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
    /// <summary>Returns a page of activity log entries together with the total entry count.</summary>
    Task<(List<ActivityLogEntity> Items, int Total)> GetActivityLogs(int page);
}

/// <summary>Provides CRUD operations for messages, drafts, notes, and activity log entries stored in the local database.</summary>
public sealed class EntryService : IEntryService
{
    private readonly IMessageRepository _messages;
    private readonly IDraftRepository _drafts;
    private readonly INoteRepository _notes;
    private readonly IActivityLogRepository _activityLogs;
    private readonly IFolderRepository _folders;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IMessageFormat _messageFormat;
    private readonly SemaphoreSlim _deliveryLock = new(1, 1);

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

    /// <summary>Initializes a new <see cref="EntryService"/> with the required repositories and providers.</summary>
    public EntryService(
        IMessageRepository messages,
        IDraftRepository drafts,
        INoteRepository notes,
        IActivityLogRepository activityLogs,
        IFolderRepository folders,
        ICurrentUserProvider currentUserProvider,
        IMessageFormat messageFormat)
    {
        _messages = messages;
        _drafts = drafts;
        _notes = notes;
        _activityLogs = activityLogs;
        _folders = folders;
        _currentUserProvider = currentUserProvider;
        _messageFormat = messageFormat;
    }

    private object BuildMessage(string messageId, string fromUser, string subject, string body, List<AddressData> addresses, DateTime sentAt, bool isAlert, int priority, string tag)
    {
        object message = _messageFormat.CreateMessage();
        _messageFormat.SetMessageId(message, messageId);
        _messageFormat.SetFromUser(message, fromUser);
        _messageFormat.SetSubject(message, subject);
        _messageFormat.SetBody(message, body);
        _messageFormat.SetAddresses(message, addresses.Select(a => new MessageAddress { UserName = a.UserName, Type = a.Type.ParseAddressType() }).ToList());
        _messageFormat.SetSentAt(message, sentAt);
        _messageFormat.SetIsAlert(message, isAlert);
        _messageFormat.SetPriority(message, priority);
        _messageFormat.SetTag(message, tag);
        return message;
    }

    /// <summary>Persists a sent message to the Outbox folder, including per-user delivery status entries.</summary>
    public async Task<MessageEntity> StoreSentMessage(string messageId, string subject, string body, List<AddressData> addresses, DateTime sentAt, IReadOnlyList<UserDeliveryResult> userResults, bool isAlert = false, int priority = 0, string tag = "")
    {
        string outboxId = await _folders.GetRootId(FolderType.Outbox);
        List<DeliveryStatus> deliveryStatuses = userResults
            .Select(r => new DeliveryStatus
            {
                UserName = r.UserName,
                Status = r.Success ? DestinationStatus.Confirmed : DestinationStatus.Failed,
                AddressedVia = [.. r.AddressedVia]
            })
            .ToList();
        MessageEntity entity = new()
        {
            MessageId = messageId,
            Message = BuildMessage(messageId, _currentUserProvider.UserName ?? string.Empty, subject, body, addresses, sentAt, isAlert, priority, tag),
            DeliveryStatuses = deliveryStatuses,
            ReceivedAt = sentAt,
            FolderId = outboxId,
            IsOutbound = true
        };
        await _messages.Insert(entity);
        return entity;
    }

    /// <summary>Updates the delivery status for a specific user on an existing message entity.</summary>
    public async Task<MessageEntity?> UpdateDeliveryStatus(string messageId, string userName, DestinationStatus status)
    {
        await _deliveryLock.WaitAsync();
        try
        {
            // Delivery status always applies to the Outbox (sent) record. A self-addressed message also has
            // an Inbox (received) record sharing the same MessageId, which must never receive this update.
            MessageEntity? entity = await _messages.Get(messageId, outbound: true);
            if (entity is null) return null;

            DeliveryStatus? existing = entity.DeliveryStatuses.FirstOrDefault(d => d.UserName == userName);
            if (existing is not null)
                existing.Status = status;
            else
                entity.DeliveryStatuses.Add(new DeliveryStatus { UserName = userName, Status = status });

            await _messages.Update(entity);
            return entity;
        }
        finally
        {
            _deliveryLock.Release();
        }
    }

    /// <summary>Persists a received message to the Inbox folder and raises <see cref="MessageInserted"/>.</summary>
    public async Task<MessageEntity> StoreIncomingMessage(string messageId, string fromUser, string subject, string body, List<AddressData> addresses, DateTime sentAt, bool isAlert = false, int priority = 0, string tag = "")
    {
        string inboxId = await _folders.GetRootId(FolderType.Inbox);
        MessageEntity entity = new()
        {
            MessageId = messageId,
            Message = BuildMessage(messageId, fromUser, subject, body, addresses, sentAt, isAlert, priority, tag),
            ReceivedAt = sentAt,
            FolderId = inboxId,
            ReadStatus = DestinationStatus.Received
        };

        await _messages.Insert(entity);

        if (MessageInserted is not null)
            await MessageInserted(entity);

        return entity;
    }

    /// <summary>
    /// Transitions the Inbox record for <paramref name="messageId"/> from <see cref="DestinationStatus.Received"/>
    /// to <see cref="DestinationStatus.Read"/> and raises <see cref="MessageRead"/>. Returns <see langword="null"/>
    /// (a no-op) if the record does not exist or is already <see cref="DestinationStatus.Read"/>.
    /// </summary>
    public async Task<MessageEntity?> MarkMessageRead(string messageId)
    {
        MessageEntity? entity = await _messages.Get(messageId, outbound: false);
        if (entity is null || entity.ReadStatus != DestinationStatus.Received) return null;

        entity.ReadStatus = DestinationStatus.Read;
        await _messages.Update(entity);

        if (MessageRead is not null)
            await MessageRead(entity);

        return entity;
    }

    /// <summary>Creates a new empty draft in the Drafts folder and raises <see cref="DraftInserted"/>.</summary>
    public async Task<DraftEntity> CreateDraft()
    {
        string draftsId = await _folders.GetRootId(FolderType.Drafts);
        DraftEntity entity = new() { FolderId = draftsId };
        await _drafts.Insert(entity);

        if (DraftInserted is not null)
            await DraftInserted(entity);

        return entity;
    }

    /// <summary>Creates a new empty note in the Notes folder and raises <see cref="NoteInserted"/>.</summary>
    public async Task<NoteEntity> CreateNote()
    {
        string notesId = await _folders.GetRootId(FolderType.Notes);
        NoteEntity entity = new() { FolderId = notesId };
        await _notes.Insert(entity);

        if (NoteInserted is not null)
            await NoteInserted(entity);

        return entity;
    }

    /// <summary>Persists changes to an existing draft and raises <see cref="DraftUpdated"/> if it has not yet been sent.</summary>
    public async Task SaveDraft(DraftEntity entity)
    {
        entity.ModifiedAt = DateTime.UtcNow;
        await _drafts.Update(entity);
        if (!entity.IsSent && DraftUpdated is not null)
            await DraftUpdated(entity);
    }

    /// <summary>Persists changes to an existing note and raises <see cref="NoteUpdated"/>.</summary>
    public async Task SaveNote(NoteEntity entity)
    {
        entity.ModifiedAt = DateTime.UtcNow;
        await _notes.Update(entity);
        if (NoteUpdated is not null)
            await NoteUpdated(entity);
    }

    /// <summary>Returns a page of messages from the specified folder together with the total message count.</summary>
    public async Task<(List<MessageEntity> Items, int Total)> GetMessages(string folderId, int page)
    {
        List<MessageEntity> items = await _messages.GetPage(folderId, page);
        int total = await _messages.Count(folderId);
        return (items, total);
    }

    /// <summary>Returns a page of drafts from the specified folder together with the total draft count.</summary>
    public async Task<(List<DraftEntity> Items, int Total)> GetDrafts(string folderId, int page, bool alphabetical)
    {
        List<DraftEntity> items = await _drafts.GetPage(folderId, page, alphabetical);
        int total = await _drafts.Count(folderId);
        return (items, total);
    }

    /// <summary>Returns a page of notes from the specified folder together with the total note count.</summary>
    public async Task<(List<NoteEntity> Items, int Total)> GetNotes(string folderId, int page, bool alphabetical)
    {
        List<NoteEntity> items = await _notes.GetPage(folderId, page, alphabetical);
        int total = await _notes.Count(folderId);
        return (items, total);
    }

    /// <inheritdoc />
    public async Task DeleteEntry(string id, EntryType entryType, bool isOutboundMessage = false)
    {
        switch (entryType)
        {
            case EntryType.Message:
                await _messages.Delete(id, isOutboundMessage);
                break;
            case EntryType.Draft:
                try { await _drafts.Delete(new ObjectId(id)); } catch { }
                break;
            case EntryType.Note:
                try { await _notes.Delete(new ObjectId(id)); } catch { }
                break;
        }
    }

    /// <inheritdoc />
    public async Task MoveEntry(string entryId, EntryType entryType, string targetFolderId, bool isOutboundMessage = false)
    {
        switch (entryType)
        {
            case EntryType.Message:
                MessageEntity? msg = await _messages.Get(entryId, isOutboundMessage);
                if (msg is not null) { msg.FolderId = targetFolderId; await _messages.Update(msg); }
                break;
            case EntryType.Draft:
                DraftEntity? draft = await _drafts.Get(new ObjectId(entryId));
                if (draft is not null) { draft.FolderId = targetFolderId; await _drafts.Update(draft); }
                break;
            case EntryType.Note:
                NoteEntity? note = await _notes.Get(new ObjectId(entryId));
                if (note is not null) { note.FolderId = targetFolderId; await _notes.Update(note); }
                break;
        }
    }

    /// <summary>Returns a page of activity log entries together with the total entry count.</summary>
    public async Task<(List<ActivityLogEntity> Items, int Total)> GetActivityLogs(int page)
    {
        List<ActivityLogEntity> items = await _activityLogs.GetPage(page);
        int total = await _activityLogs.Count();
        return (items, total);
    }
}
