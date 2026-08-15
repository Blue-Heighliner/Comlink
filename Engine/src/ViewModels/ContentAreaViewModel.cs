namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>ViewModel interface for the main content area that displays the active entry or home screen.</summary>
public interface IContentAreaViewModel
{
    /// <summary>Raised when a draft is successfully sent and produces a message entity.</summary>
    event Func<MessageEntity, Task>? DraftSent;

    /// <summary>Gets or sets the currently displayed entry ViewModel, or <see langword="null"/> when showing the home screen.</summary>
    object? ActiveContent { get; set; }
    /// <summary>Gets or sets a value indicating whether the home screen placeholder is visible.</summary>
    bool IsHomeVisible { get; set; }
    /// <summary>Gets the welcome text supplied by the host's home content provider.</summary>
    string HomeText { get; }

    /// <summary>Resets the content area to the home screen.</summary>
    void ShowHome();
    /// <summary>Loads and displays the full entry ViewModel for the given entry item.</summary>
    Task ShowEntry(EntryItemViewModel entry);
    /// <summary>Displays an already-constructed entry ViewModel directly.</summary>
    void ShowEntry(object entryVm);
}

/// <summary>ViewModel for the main content area that displays the active entry or home screen.</summary>
public sealed partial class ContentAreaViewModel : ObservableObject, IContentAreaViewModel
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
    public ContentAreaViewModel(
        IEngineController engineController,
        IEntryService entryService,
        IServiceConnection connection,
        IMessageRepository messages,
        IDraftRepository drafts,
        INoteRepository notes,
        IActivityLogRepository activityLogs,
        ILoggerFactory loggerFactory)
    {
        this.entryService = entryService;
        this.connection = connection;
        this.messages = messages;
        this.drafts = drafts;
        this.notes = notes;
        this.activityLogs = activityLogs;
        this.engineController = engineController;
        this.loggerFactory = loggerFactory;
        HomeText = engineController.HomeText;
        connection.DeliveryStatusChanged += OnDeliveryStatusChanged;
    }

    private readonly IEntryService entryService;
    private readonly IServiceConnection connection;
    private readonly IMessageRepository messages;
    private readonly IDraftRepository drafts;
    private readonly INoteRepository notes;
    private readonly IActivityLogRepository activityLogs;
    private readonly IEngineController engineController;
    private readonly ILoggerFactory loggerFactory;

    [ObservableProperty] private object? activeContent;
    [ObservableProperty] private bool isHomeVisible = true;

    /// <summary>Raised when a draft is successfully sent and produces a message entity.</summary>
    public event Func<MessageEntity, Task>? DraftSent;
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
        ActiveContent = null;
        IsHomeVisible = true;
    }

    /// <summary>Loads and displays the full entry ViewModel for the given entry item.</summary>
    public async Task ShowEntry(EntryItemViewModel entry)
    {
        IsHomeVisible = false;
        ActiveContent = await BuildEntryViewModel(entry);
    }

    /// <summary>Displays an already-constructed entry ViewModel directly.</summary>
    public void ShowEntry(object entryVm)
    {
        IsHomeVisible = false;
        ActiveContent = entryVm;
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
        if (entity is null) { return null; }

        if (!entity.IsOutbound && entity.ReadStatus == DestinationStatus.Received)
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
        if (oid is null) { return null; }
        DraftEntity? entity = await drafts.Get(oid);
        if (entity is null) { return null; }
        List<string> userNames = await connection.GetUserNames();
        DraftViewModel vm = new(entity, entryService, connection, userNames, loggerFactory, engineController);
        vm.DraftSent += async (IDraftViewModel _, MessageEntity msg) =>
        {
            ShowEntry(new MessageViewModel(msg, engineController));
            if (DraftSent is not null) { await DraftSent(msg); }
        };
        return vm;
    }

    private async Task<NoteViewModel?> BuildNoteViewModel(string id)
    {
        ObjectId? oid = TryParseObjectId(id);
        if (oid is null) { return null; }
        NoteEntity? entity = await notes.Get(oid);
        return entity is null ? null : new NoteViewModel(entity, entryService);
    }

    private async Task<ActivityLogViewModel?> BuildActivityLogViewModel(string id)
    {
        ObjectId? oid = TryParseObjectId(id);
        if (oid is null) { return null; }
        ActivityLogEntity? entity = await activityLogs.Get(oid);
        return entity is null ? null : new ActivityLogViewModel(entity);
    }
}
