namespace BlueHeighliner.Comlink;

/// <summary>ViewModel interface for the main content area that displays the active entry or home screen.</summary>
internal interface IContentAreaViewModel
{
    /// <summary>Raised when a draft is successfully sent and produces a message entity.</summary>
    event Func<MessageEntity, Task>? DraftSent;
    /// <summary>Raised after the draft or note shown in the content area is deleted from its editor, once the content area has returned to the home screen.</summary>
    event Func<Task>? EntryDeleted;
    /// <summary>Raised with the id, type and new title (its name or first line) when the draft or note shown in the content area is edited in a way that changes what the list calls it.</summary>
    event Action<string, EntryType, string>? EntryTitleChanged;

    /// <summary>Raised with the type, identifier and direction (whether it is a sent message; meaningless otherwise) of a message, draft or note after it has been opened in the content area, so the listings can show where it is.</summary>
    event Func<EntryType, string, bool, Task>? EntryOpened;

    /// <summary>Gets or sets the currently displayed entry ViewModel, or <see langword="null"/> when showing the home screen.</summary>
    object? ActiveContent { get; set; }
    /// <summary>Gets or sets a value indicating whether the home screen placeholder is visible.</summary>
    bool IsHomeVisible { get; set; }
    /// <summary>Gets the welcome text supplied by the host's home content provider.</summary>
    string HomeText { get; }

    /// <summary>Resets the content area to the home screen. Discards the staged send queue if it was showing (see <see cref="StagedSendViewModel"/>).</summary>
    void ShowHome();
    /// <summary>Loads and displays the full entry ViewModel for the given entry item. Discards the staged send queue if it was showing (see <see cref="StagedSendViewModel"/>).</summary>
    Task ShowEntry(EntryItemViewModel entry);
    /// <summary>Loads and displays the draft with the given id, saving the editor it replaces first.</summary>
    Task ShowDraft(string id);
    /// <summary>Loads and displays the note with the given id, saving the editor it replaces first.</summary>
    Task ShowNote(string id);
    /// <summary>Displays an already-constructed entry ViewModel directly. Discards the staged send queue if it was showing (see <see cref="StagedSendViewModel"/>).</summary>
    void ShowEntry(object entryVm);
}

/// <summary>ViewModel for the main content area that displays the active entry or home screen.</summary>
internal sealed partial class ContentAreaViewModel : ObservableObject, IContentAreaViewModel
{
    private static ObjectId? TryParseObjectId(string id)
    {
        try { return new ObjectId(id); }
        catch { return null; }
    }

    /// <summary>Initializes a new <see cref="ContentAreaViewModel"/> with the required repositories and services.</summary>
    /// <param name="engineController">Provides the home screen welcome text, the shared alert label text, whether the alert checkbox is shown, and message composition settings.</param>
    /// <param name="entryService">Entry service for save and send operations.</param>
    /// <param name="connection">Service connection for delivery status events.</param>
    /// <param name="messages">Repository for loading message entries.</param>
    /// <param name="drafts">Repository for loading draft entries.</param>
    /// <param name="notes">Repository for loading note entries.</param>
    /// <param name="activityLogs">Repository for loading activity log entries.</param>
    /// <param name="loggerFactory">Factory for creating named loggers.</param>
    /// <param name="currentUserProvider">Tracks the current user's name, read to resolve their own message level for a newly opened draft.</param>
    /// <param name="stagedSend">The staged send ViewModel; its queue is discarded whenever the content area navigates away from it.</param>
    /// <param name="bodyDocumentFactory">Factory for the body document of a draft opened from the list; must match the one used for new drafts, or the draft editor cannot bind it. Defaults to plain string documents when <see langword="null"/>.</param>
    public ContentAreaViewModel(
        IEngineController engineController,
        IEntryService entryService,
        IEngineConnection connection,
        IMessageRepository messages,
        IDraftRepository drafts,
        INoteRepository notes,
        IActivityLogRepository activityLogs,
        ILoggerFactory loggerFactory,
        ICurrentUserProvider currentUserProvider,
        IStagedSendViewModel stagedSend,
        IBodyDocumentFactory? bodyDocumentFactory = null)
    {
        this.bodyDocumentFactory = bodyDocumentFactory;
        this.entryService = entryService;
        this.connection = connection;
        this.messages = messages;
        this.drafts = drafts;
        this.notes = notes;
        this.activityLogs = activityLogs;
        this.engineController = engineController;
        this.loggerFactory = loggerFactory;
        logger = loggerFactory.CreateLogger(LogCategories.App);
        this.currentUserProvider = currentUserProvider;
        this.stagedSend = stagedSend;
        HomeText = engineController.HomeText;
        connection.DeliveryStatusChanged += evt => UiThread.Run(() => OnDeliveryStatusChanged(evt));
    }

    private readonly IEntryService entryService;
    private readonly IEngineConnection connection;
    private readonly IMessageRepository messages;
    private readonly IDraftRepository drafts;
    private readonly INoteRepository notes;
    private readonly IActivityLogRepository activityLogs;
    private readonly IEngineController engineController;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger logger;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly IStagedSendViewModel stagedSend;
    private readonly IBodyDocumentFactory? bodyDocumentFactory;

    [ObservableProperty] private object? activeContent;
    [ObservableProperty] private bool isHomeVisible = true;
    private int showGeneration;

    /// <summary>Raised when a draft is successfully sent and produces a message entity.</summary>
    public event Func<MessageEntity, Task>? DraftSent;
    /// <inheritdoc />
    public event Func<Task>? EntryDeleted;
    /// <inheritdoc />
    public event Func<EntryType, string, bool, Task>? EntryOpened;
    /// <inheritdoc />
    public event Action<string, EntryType, string>? EntryTitleChanged;
    /// <summary>Gets the welcome text supplied by the host's home content provider.</summary>
    public string HomeText { get; }

    private Task OnDeliveryStatusChanged(DeliveryStatusChangedEvent evt)
    {
        if (ActiveContent is not IMessageViewModel msgVm || msgVm.MessageId != evt.MessageId)
        {
            return Task.CompletedTask;
        }

        // An empty UserName marks a local read-status notification for this user's own Inbox record
        // (see DirectServiceConnection.MarkMessageRead), not a remote destination's delivery status.
        if (string.IsNullOrEmpty(evt.UserName))
        {
            msgVm.ReadStatus = evt.Status;
        }
        else
        {
            msgVm.UpdateDeliveryStatus(evt.UserName, evt.Status);
        }
        return Task.CompletedTask;
    }

    /// <summary>Resets the content area to the home screen.</summary>
    public void ShowHome()
    {
        _ = SaveLeavingEditor();
        DiscardStagedSendIfLeaving();
        showGeneration++;
        ActiveContent = null;
        IsHomeVisible = true;
    }

    /// <summary>Loads and displays the full entry ViewModel for the given entry item.</summary>
    public async Task ShowEntry(EntryItemViewModel entry)
    {
        // Loading takes a database round trip; if the user has moved on to another entry (or home) by the time it
        // finishes, this older load must not replace what they chose afterwards.
        int generation = ++showGeneration;
        IsHomeVisible = false;
        await SaveLeavingEditor();
        object? content = await BuildEntryViewModel(entry);
        if (generation == showGeneration)
        {
            DiscardStagedSendIfLeaving();
            ActiveContent = content;
            await NotifyOpened();
        }
    }

    /// <inheritdoc />
    public Task ShowDraft(string id) => ShowBuilt(BuildDraftViewModel(id));

    /// <inheritdoc />
    public Task ShowNote(string id) => ShowBuilt(BuildNoteViewModel(id));

    private async Task ShowBuilt<T>(Task<T?> build)
        where T : class
    {
        int generation = ++showGeneration;
        IsHomeVisible = false;
        await SaveLeavingEditor();
        T? content = await build;
        if (generation == showGeneration)
        {
            DiscardStagedSendIfLeaving();
            ActiveContent = content;
            await NotifyOpened();
        }
    }

    /// <summary>Displays an already-constructed entry ViewModel directly.</summary>
    public void ShowEntry(object entryVm)
    {
        _ = SaveLeavingEditor();
        DiscardStagedSendIfLeaving();
        showGeneration++;
        IsHomeVisible = false;
        ActiveContent = entryVm;
        _ = NotifyOpened();
    }

    private async Task NotifyOpened()
    {
        if (EntryOpened is null)
        {
            return;
        }

        (EntryType Type, string Id, bool IsOutbound)? opened = ActiveContent switch
        {
            IMessageViewModel message => (EntryType.Message, message.MessageId, message.IsOutbound),
            IDraftViewModel draft => (EntryType.Draft, draft.Id, false),
            INoteViewModel note => (EntryType.Note, note.Id, false),
            _ => null
        };
        if (opened is not var (type, id, isOutbound))
        {
            return;
        }

        try { await EntryOpened.InvokeAll(type, id, isOutbound); }
        catch (Exception ex) { logger.Record(LogEvents.RevealOpenedEntryFailed, ex, "Failed to show where the opened entry is kept"); }
    }

    // The staged send screen only ever exists as the automatic result of an import - there is no way to navigate
    // to it, so navigating anywhere else (a folder, an entry, another screen, or home) is the only way to leave
    // it, and always means the user is done with it - discard whatever it was still holding rather than leaving
    // it to reappear stale next time an import triggers it.
    private void DiscardStagedSendIfLeaving()
    {
        if (ReferenceEquals(ActiveContent, stagedSend))
        {
            stagedSend.ClearCommand.Execute(null);
        }
    }

    // A draft or note the user is leaving is saved as it is, so what was written is there when it is opened again, without having to press SAVE.
    private async Task SaveLeavingEditor()
    {
        try
        {
            switch (ActiveContent)
            {
                case IDraftViewModel draft: await draft.SaveChanges(); break;
                case INoteViewModel note: await note.SaveChanges(); break;
            }
        }
        catch (Exception ex)
        {
            logger.Record(LogEvents.SaveOnLeavingFailed, ex, "Failed to save what was written before leaving it");
            logger.Record(LogEvents.EntryNotSaved, "What you were writing could not be saved");
        }
    }

    private async Task<object?> BuildEntryViewModel(EntryItemViewModel item)
    {
        return item.EntryType switch
        {
            EntryType.Message => await BuildMessageViewModel(item.Id, item.IsOutboundMessage),
            EntryType.Draft => await BuildDraftViewModel(item.Id),
            EntryType.Note => await BuildNoteViewModel(item.Id),
            EntryType.Activity => await BuildActivityLogViewModel(item.Id),
            _ => null
        };
    }

    private async Task<MessageViewModel?> BuildMessageViewModel(string id, bool isOutboundMessage)
    {
        MessageEntity? entity = await messages.Get(id, isOutboundMessage);
        if (entity is null)
        {
            return null;
        }

        if (!entity.IsOutbound && entity.ReadStatus is DestinationStatus.Received)
        {
            try
            {
                if (await connection.MarkMessageRead(entity.MessageId))
                {
                    entity.ReadStatus = DestinationStatus.Read;
                }
            }
            catch { }
        }

        return new MessageViewModel(entity, engineController);
    }

    private async Task<DraftViewModel?> BuildDraftViewModel(string id)
    {
        ObjectId? oid = TryParseObjectId(id);
        if (oid is null)
        {
            return null;
        }
        DraftEntity? entity = await drafts.Get(oid);
        if (entity is null)
        {
            return null;
        }
        List<string> userNames = await connection.GetUserNames();
        string currentMessageLevel = engineController.GetUserMessageLevel(currentUserProvider.UserName ?? string.Empty);
        DraftViewModel vm = new(entity, entryService, connection, userNames, loggerFactory, engineController, bodyDocumentFactory?.Create(), currentMessageLevel: currentMessageLevel);
        vm.DraftSent += async (IDraftViewModel _, MessageEntity msg) =>
        {
            ShowEntry(new MessageViewModel(msg, engineController));
            if (DraftSent is not null)
            {
                await DraftSent(msg);
            }
        };
        vm.Deleted += HandleEditorDeleted;
        vm.Duplicated += ShowDraft;
        vm.TitleChanged += title => EntryTitleChanged?.Invoke(vm.Id, EntryType.Draft, title);
        return vm;
    }

    private async Task<NoteViewModel?> BuildNoteViewModel(string id)
    {
        ObjectId? oid = TryParseObjectId(id);
        if (oid is null)
        {
            return null;
        }
        NoteEntity? entity = await notes.Get(oid);
        if (entity is null)
        {
            return null;
        }

        NoteViewModel vm = new(entity, entryService, engineController.CanDelete(FolderType.Notes));
        vm.Deleted += HandleEditorDeleted;
        vm.Duplicated += ShowNote;
        vm.TitleChanged += title => EntryTitleChanged?.Invoke(vm.Id, EntryType.Note, title);
        return vm;
    }

    private async Task HandleEditorDeleted()
    {
        ShowHome();
        if (EntryDeleted is not null)
        {
            await EntryDeleted();
        }
    }

    private async Task<ActivityLogViewModel?> BuildActivityLogViewModel(string id)
    {
        ObjectId? oid = TryParseObjectId(id);
        if (oid is null)
        {
            return null;
        }
        ActivityLogEntity? entity = await activityLogs.Get(oid);
        return entity is null ? null : new ActivityLogViewModel(entity, engineController.LogWidths.Id);
    }
}
