namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>ViewModel interface for the main application window.</summary>
public interface IMainViewModel
{
    /// <summary>Gets or sets a value indicating whether the install screen is visible instead of the main UI.</summary>
    bool IsInstallScreenVisible { get; set; }
    /// <summary>Gets or sets a value indicating whether the UI is running in kiosk mode.</summary>
    bool IsKioskMode { get; set; }
    /// <summary>Gets or sets the local site name displayed in the title bar.</summary>
    string SiteName { get; set; }
    /// <summary>Gets or sets the environment title displayed in the title bar.</summary>
    string EnvironmentTitle { get; set; }
    /// <summary>Gets or sets the environment accent color as a hex string.</summary>
    string EnvironmentColor { get; set; }
    /// <summary>Gets or sets the application version string.</summary>
    string AppVersion { get; set; }
    /// <summary>Gets the folder bar ViewModel.</summary>
    IFolderBarViewModel FolderBar { get; }
    /// <summary>Gets the entry bar ViewModel.</summary>
    IEntryBarViewModel EntryBar { get; }
    /// <summary>Gets the content area ViewModel.</summary>
    IContentAreaViewModel ContentArea { get; }
    /// <summary>Gets the install screen ViewModel.</summary>
    IInstallViewModel InstallView { get; }
    /// <summary>Creates a new draft and displays it in the content area.</summary>
    IAsyncRelayCommand CreateDraftCommand { get; }
    /// <summary>Creates a new note and displays it in the content area.</summary>
    IAsyncRelayCommand CreateNoteCommand { get; }
    /// <summary>Connects to the service, loads site info, and initializes either the main UI or the install screen.</summary>
    Task Initialize();
}

/// <summary>Root ViewModel for the main application window, coordinating folder, entry, and content area ViewModels.</summary>
public sealed partial class MainViewModel : ObservableObject, IMainViewModel
{
    private readonly IServiceConnection _connection;
    private readonly ILiteDbContext _db;
    private readonly IEntryService _entryService;
    private readonly IFolderBarViewModel _folderBar;
    private readonly IEntryBarViewModel _entryBar;
    private readonly IContentAreaViewModel _contentArea;
    private readonly IInstallViewModel _installViewModel;
    private readonly ICurrentSiteProvider _currentSiteProvider;
    private readonly IAppNameProvider _appNameProvider;
    private readonly IBodyDocumentFactory _bodyDocumentFactory;
    private readonly IMessageFormat _messageFormat;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly ILogger _activityLogger;

    [ObservableProperty] private bool _isInstallScreenVisible;
    [ObservableProperty] private bool _isKioskMode;
    [ObservableProperty] private string _siteName = string.Empty;
    [ObservableProperty] private string _environmentTitle = string.Empty;
    [ObservableProperty] private string _environmentColor = "#1565C0";
    [ObservableProperty] private string _appVersion;

    /// <inheritdoc />
    public IFolderBarViewModel FolderBar => _folderBar;
    /// <inheritdoc />
    public IEntryBarViewModel EntryBar => _entryBar;
    /// <inheritdoc />
    public IContentAreaViewModel ContentArea => _contentArea;
    /// <inheritdoc />
    public IInstallViewModel InstallView => _installViewModel;

    /// <summary>Initializes a new <see cref="MainViewModel"/> with all required engine and UI dependencies.</summary>
    /// <param name="connection">Service connection used for site and messaging operations.</param>
    /// <param name="db">LiteDB context for lazy initialization after install.</param>
    /// <param name="entryService">Entry CRUD service for messages, drafts, and notes.</param>
    /// <param name="folderBar">Folder tree ViewModel.</param>
    /// <param name="entryBar">Entry list ViewModel.</param>
    /// <param name="contentArea">Content area ViewModel.</param>
    /// <param name="installViewModel">Install screen ViewModel.</param>
    /// <param name="currentSiteProvider">Provides and accepts the current site name.</param>
    /// <param name="appNameProvider">Provides the application display name.</param>
    /// <param name="kioskModeProvider">Determines whether the UI should run in kiosk mode.</param>
    /// <param name="loggerFactory">Factory for creating named loggers.</param>
    /// <param name="bodyDocumentFactory">Factory for creating the body document for new drafts.</param>
    /// <param name="messageFormat">Maps logical fields onto a message entity's stored message.</param>
    public MainViewModel(
        IServiceConnection connection,
        ILiteDbContext db,
        IEntryService entryService,
        IFolderBarViewModel folderBar,
        IEntryBarViewModel entryBar,
        IContentAreaViewModel contentArea,
        IInstallViewModel installViewModel,
        ICurrentSiteProvider currentSiteProvider,
        IAppNameProvider appNameProvider,
        IKioskModeProvider kioskModeProvider,
        ILoggerFactory loggerFactory,
        IBodyDocumentFactory bodyDocumentFactory,
        IMessageFormat messageFormat)
    {
        _connection = connection;
        _db = db;
        _entryService = entryService;
        _folderBar = folderBar;
        _entryBar = entryBar;
        _contentArea = contentArea;
        _installViewModel = installViewModel;
        _currentSiteProvider = currentSiteProvider;
        _appNameProvider = appNameProvider;
        _bodyDocumentFactory = bodyDocumentFactory;
        _messageFormat = messageFormat;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger("APP");
        _activityLogger = loggerFactory.CreateLogger("ACTIVITY");

        _isKioskMode = kioskModeProvider.IsKioskMode;
        _appVersion = GetAppVersion();
        WireEvents();
    }

    private void WireEvents()
    {
        _folderBar.FolderSelected += async folder =>
        {
            _contentArea.ShowHome();
            await _entryBar.LoadFolder(folder);
        };

        _folderBar.EntryMoved += async () =>
            await _entryBar.Refresh();

        _entryBar.EntrySelected += async entry =>
            await _contentArea.ShowEntry(entry);

        _installViewModel.InstallSucceeded += async info =>
        {
            _db.Initialize();
            _currentSiteProvider.SiteName = info.Name;
            await ApplySiteInfo(info);
            await StartMainUi();
            _logger.LogInformation("{AppName} started", _appNameProvider.AppName);
            IsInstallScreenVisible = false;
        };

        _contentArea.DraftSent += HandleDraftSent;

        _connection.DeliveryStatusChanged += async evt =>
        {
            await _entryBar.UpdateEntryStatus(evt.MessageId, evt.OverallStatus);
        };

        _connection.MessageReceived += async evt =>
        {
            try
            {
                MessageEntity entity = await _entryService.StoreIncomingMessage(
                    evt.MessageId, evt.FromSite, evt.Subject, evt.Body,
                    evt.Addresses.Select(a => new Data.Entities.AddressData { SiteName = a.SiteName, Type = a.Type }).ToList(),
                    evt.SentAt);

                FolderItemViewModel? inboxFolder = _folderBar.RootFolders.FirstOrDefault(f => f.RootType == FolderType.Inbox);
                if (inboxFolder is not null && _folderBar.SelectedFolder?.Id == inboxFolder.Id)
                {
                    string timeText = entity.ReceivedAt.ToString("dd-MMM-yyyy HH:mm").ToUpperInvariant();
                    EntryItemViewModel item = new(entity.MessageId, evt.FromSite, EntryType.Message, entity.ReceivedAt,
                        secondaryText: evt.Subject, timeText: timeText, fixedStatusText: "RECEIVED");
                    await _entryBar.PrependEntry(item);
                }
            }
            catch (Exception ex)
            {
                _activityLogger.LogError(ex, "Failed to store received message from {FromSite}", evt.FromSite);
            }
        };

        _entryService.DraftUpdated += async entity =>
        {
            _entryBar.SetPendingSelectId(entity.Id.ToString());
            FolderItemViewModel? draftsFolder = _folderBar.RootFolders.FirstOrDefault(f => f.RootType == FolderType.Drafts);
            if (draftsFolder is null) return;
            if (_folderBar.SelectedFolder?.Id == draftsFolder.Id)
                await _entryBar.Refresh();
            else
                _folderBar.SelectFolderByType(FolderType.Drafts);
        };

        _entryService.NoteUpdated += async entity =>
        {
            _entryBar.SetPendingSelectId(entity.Id.ToString());
            FolderItemViewModel? notesFolder = _folderBar.RootFolders.FirstOrDefault(f => f.RootType == FolderType.Notes);
            if (notesFolder is null) return;
            if (_folderBar.SelectedFolder?.Id == notesFolder.Id)
                await _entryBar.Refresh();
            else
                _folderBar.SelectFolderByType(FolderType.Notes);
        };
    }

    /// <inheritdoc />
    public async Task Initialize()
    {
        try
        {
            await _connection.Connect();
            SiteInfo? siteInfo = await _connection.GetSiteInfo();

            if (siteInfo is not null)
            {
                _db.Initialize();
                _currentSiteProvider.SiteName = siteInfo.Name;
                await ApplySiteInfo(siteInfo);
                await StartMainUi();
            }
            else
            {
                IsInstallScreenVisible = true;
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Initialization failed"); }
    }

    private async Task StartMainUi()
    {
        await _folderBar.Load();
    }

    private Task ApplySiteInfo(SiteInfo info)
    {
        SiteName = info.Name;
        EnvironmentTitle = info.EnvironmentTitle;
        EnvironmentColor = info.EnvironmentColor;
        return Task.CompletedTask;
    }

    private async Task HandleDraftSent(MessageEntity msg)
    {
        FolderItemViewModel? outboxFolder = _folderBar.RootFolders.FirstOrDefault(f => f.RootType == FolderType.Outbox);
        if (outboxFolder is null) return;

        _entryBar.SetPendingSelectId(msg.MessageId);
        if (_folderBar.SelectedFolder?.Id == outboxFolder.Id)
            await _entryBar.Refresh();
        else
            _folderBar.SelectFolderByType(FolderType.Outbox);
    }

    [RelayCommand]
    private async Task CreateDraft()
    {
        DraftEntity entity = await _entryService.CreateDraft();
        List<string> siteNames = await _connection.GetSiteNames();
        Entries.DraftViewModel vm = new(entity, _entryService, _connection, siteNames, _loggerFactory, _bodyDocumentFactory.Create());
        vm.DraftSent += async (IDraftViewModel _, MessageEntity msg) =>
        {
            _contentArea.ShowEntry(new Entries.MessageViewModel(msg, _messageFormat));
            await HandleDraftSent(msg);
        };
        _contentArea.ShowEntry(vm);
    }

    [RelayCommand]
    private async Task CreateNote()
    {
        NoteEntity entity = await _entryService.CreateNote();
        Entries.NoteViewModel vm = new(entity, _entryService);
        _contentArea.ShowEntry(vm);
    }

    private static string GetAppVersion()
    {
        Version? version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
        return version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
