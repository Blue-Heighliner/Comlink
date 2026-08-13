namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>ViewModel interface for the main content area that displays the active entry or home screen.</summary>
public interface IContentAreaViewModel
{
    /// <summary>Gets or sets the currently displayed entry ViewModel, or <see langword="null"/> when showing the home screen.</summary>
    object? ActiveContent { get; set; }
    /// <summary>Gets or sets a value indicating whether the home screen placeholder is visible.</summary>
    bool IsHomeVisible { get; set; }
    /// <summary>Gets the welcome text supplied by the host's home content provider.</summary>
    string HomeText { get; }
    /// <summary>Raised when a draft is successfully sent and produces a message entity.</summary>
    event Func<MessageEntity, Task>? DraftSent;
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
    private readonly IHomeContentProvider _homeContent;
    private readonly IEntryService _entryService;
    private readonly IServiceConnection _connection;
    private readonly IMessageRepository _messages;
    private readonly IDraftRepository _drafts;
    private readonly INoteRepository _notes;
    private readonly IActivityLogRepository _activityLogs;
    private readonly IMessageFormat _messageFormat;
    private readonly IMessagePriorityProvider _priorityProvider;
    private readonly IAlertConfiguration _alertConfiguration;
    private readonly IAlertComposeConfiguration _alertComposeConfiguration;
    private readonly IMessageTagConfiguration _tagConfiguration;
    private readonly IMessageTagPriorityPolicy _tagPriorityPolicy;
    private readonly ILoggerFactory _loggerFactory;

    [ObservableProperty] private object? _activeContent;
    [ObservableProperty] private bool _isHomeVisible = true;

    /// <summary>Raised when a draft is successfully sent and produces a message entity.</summary>
    public event Func<MessageEntity, Task>? DraftSent;
    /// <summary>Gets the welcome text supplied by the host's home content provider.</summary>
    public string HomeText { get; }

    /// <summary>Initializes a new <see cref="ContentAreaViewModel"/> with the required repositories and services.</summary>
    /// <param name="homeContent">Provides the home screen welcome text.</param>
    /// <param name="entryService">Entry service for save and send operations.</param>
    /// <param name="connection">Service connection for delivery status events.</param>
    /// <param name="messages">Repository for loading message entries.</param>
    /// <param name="drafts">Repository for loading draft entries.</param>
    /// <param name="notes">Repository for loading note entries.</param>
    /// <param name="activityLogs">Repository for loading activity log entries.</param>
    /// <param name="messageFormat">Maps logical fields onto a message entity's stored message.</param>
    /// <param name="priorityProvider">Provides the available message priority levels to choose from.</param>
    /// <param name="alertConfiguration">Provides the shared alert label text.</param>
    /// <param name="alertComposeConfiguration">Controls whether the alert checkbox is shown.</param>
    /// <param name="tagConfiguration">Controls whether the tag input is shown.</param>
    /// <param name="tagPriorityPolicy">Provides the set of blocked tag/priority combinations enforced on send.</param>
    /// <param name="loggerFactory">Factory for creating named loggers.</param>
    public ContentAreaViewModel(
        IHomeContentProvider homeContent,
        IEntryService entryService,
        IServiceConnection connection,
        IMessageRepository messages,
        IDraftRepository drafts,
        INoteRepository notes,
        IActivityLogRepository activityLogs,
        IMessageFormat messageFormat,
        IMessagePriorityProvider priorityProvider,
        IAlertConfiguration alertConfiguration,
        IAlertComposeConfiguration alertComposeConfiguration,
        IMessageTagConfiguration tagConfiguration,
        IMessageTagPriorityPolicy tagPriorityPolicy,
        ILoggerFactory loggerFactory)
    {
        _homeContent = homeContent;
        _entryService = entryService;
        _connection = connection;
        _messages = messages;
        _drafts = drafts;
        _notes = notes;
        _activityLogs = activityLogs;
        _messageFormat = messageFormat;
        _priorityProvider = priorityProvider;
        _alertConfiguration = alertConfiguration;
        _alertComposeConfiguration = alertComposeConfiguration;
        _tagConfiguration = tagConfiguration;
        _tagPriorityPolicy = tagPriorityPolicy;
        _loggerFactory = loggerFactory;
        HomeText = _homeContent.GetHomeText();
        connection.DeliveryStatusChanged += OnDeliveryStatusChanged;
    }

    private Task OnDeliveryStatusChanged(DeliveryStatusChangedEvent evt)
    {
        if (ActiveContent is not IMessageViewModel msgVm || msgVm.MessageId != evt.MessageId)
            return Task.CompletedTask;

        // An empty UserName marks a local read-status notification for this user's own Inbox record
        // (see DirectServiceConnection.MarkMessageRead), not a remote destination's delivery status.
        if (string.IsNullOrEmpty(evt.UserName))
            msgVm.ReadStatus = evt.Status;
        else
            msgVm.UpdateDeliveryStatus(evt.UserName, evt.Status);
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
        MessageEntity? entity = await _messages.Get(id, isOutboundMessage);
        if (entity is null) return null;

        if (!entity.IsOutbound && entity.ReadStatus == DestinationStatus.Received)
        {
            try
            {
                if (await _connection.MarkMessageRead(entity.MessageId))
                    entity.ReadStatus = DestinationStatus.Read;
            }
            catch { }
        }

        return new MessageViewModel(entity, _messageFormat);
    }

    private async Task<DraftViewModel?> BuildDraftViewModel(string id)
    {
        ObjectId? oid = TryParseObjectId(id);
        if (oid is null) return null;
        DraftEntity? entity = await _drafts.Get(oid);
        if (entity is null) return null;
        List<string> userNames = await _connection.GetUserNames();
        DraftViewModel vm = new(entity, _entryService, _connection, userNames, _loggerFactory, _priorityProvider, _alertConfiguration, _alertComposeConfiguration, _tagConfiguration, _tagPriorityPolicy);
        vm.DraftSent += async (IDraftViewModel _, MessageEntity msg) =>
        {
            ShowEntry(new MessageViewModel(msg, _messageFormat));
            if (DraftSent is not null) await DraftSent(msg);
        };
        return vm;
    }

    private async Task<NoteViewModel?> BuildNoteViewModel(string id)
    {
        ObjectId? oid = TryParseObjectId(id);
        if (oid is null) return null;
        NoteEntity? entity = await _notes.Get(oid);
        return entity is null ? null : new NoteViewModel(entity, _entryService);
    }

    private async Task<ActivityLogViewModel?> BuildActivityLogViewModel(string id)
    {
        ObjectId? oid = TryParseObjectId(id);
        if (oid is null) return null;
        ActivityLogEntity? entity = await _activityLogs.Get(oid);
        return entity is null ? null : new ActivityLogViewModel(entity);
    }

    private static ObjectId? TryParseObjectId(string id)
    {
        try { return new ObjectId(id); }
        catch { return null; }
    }
}
