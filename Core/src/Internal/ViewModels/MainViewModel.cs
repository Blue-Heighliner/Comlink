namespace BlueHeighliner.Comlink;

/// <summary>ViewModel interface for the main application window.</summary>
internal interface IMainViewModel
{
    /// <summary>Gets or sets a value indicating whether the install screen is visible instead of the main UI.</summary>
    bool IsInstallScreenVisible { get; set; }
    /// <summary>Gets or sets a value indicating whether the UI is running in kiosk mode.</summary>
    bool IsKioskMode { get; set; }
    /// <summary>Gets or sets the local user name displayed in the title bar.</summary>
    string UserName { get; set; }
    /// <summary>Gets or sets the current user's security level name displayed in the title bar banner; see <see cref="IEngineController.GetUserSecurityLevel"/>.</summary>
    string SecurityLevelName { get; set; }
    /// <summary>Gets or sets the current user's security level accent color as a hex string.</summary>
    string SecurityLevelColor { get; set; }
    /// <summary>Gets or sets the application version string.</summary>
    string AppVersion { get; set; }
    /// <summary>Gets the application name, shown in the title bar's info popup.</summary>
    string AppName { get; }
    /// <summary>Gets the help ViewModel driving the help window opened from the title bar.</summary>
    IHelpViewModel Help { get; }
    /// <summary>
    /// Gets a value indicating whether this instance is running as a <see cref="UserRole.Server"/> or <see cref="UserRole.Relay"/> — a
    /// routing-only node with no inbox/outbox/notes/drafts UI of its own. When <see langword="true"/>, the
    /// main window shows either <see cref="ConnectionStatus"/>'s connections table (see
    /// <see cref="ShowConnectionsTable"/>) or the activity log view (see <see cref="ShowServerActivityView"/>),
    /// toggled via the title bar's CONNECTIONS/ACTIVITY buttons.
    /// </summary>
    bool IsServerMode { get; }
    /// <summary>
    /// Gets a value indicating whether this instance is running as a <see cref="UserRole.Client"/>. When
    /// <see langword="true"/>, the main window additionally shows a single connection-status row, from
    /// <see cref="ConnectionStatus"/>, pinned to the bottom of the window.
    /// </summary>
    bool IsClientMode { get; }
    /// <summary>Gets the connection status ViewModel — see <see cref="IsServerMode"/>/<see cref="IsClientMode"/>.</summary>
    IConnectionStatusViewModel ConnectionStatus { get; }
    /// <summary>Gets a value indicating whether the normal 3-panel folder/entry/content layout should be shown — never in <see cref="IsServerMode"/>, and never while the install screen is visible.</summary>
    bool ShowMainLayout { get; }
    /// <summary>
    /// Gets a value indicating whether the <see cref="IsServerMode"/> connections table should be shown —
    /// the view the title bar's CONNECTIONS button switches to, and the one shown by default. Mutually
    /// exclusive with <see cref="ShowServerActivityView"/>; never shown while the install screen is visible.
    /// </summary>
    bool ShowConnectionsTable { get; }
    /// <summary>
    /// Gets a value indicating whether the <see cref="IsServerMode"/> activity log view — the same
    /// <see cref="EntryBar"/>/<see cref="ContentArea"/> pairing peer/client mode shows for the Activity
    /// folder, without a folder bar — should be shown in place of <see cref="ShowConnectionsTable"/>. The
    /// view the title bar's ACTIVITY button switches to; never shown while the install screen is visible.
    /// </summary>
    bool ShowServerActivityView { get; }
    /// <summary>Gets the folder bar ViewModel.</summary>
    IFolderBarViewModel FolderBar { get; }
    /// <summary>Gets the entry bar ViewModel.</summary>
    IEntryBarViewModel EntryBar { get; }
    /// <summary>Gets the content area ViewModel.</summary>
    IContentAreaViewModel ContentArea { get; }
    /// <summary>Gets the install screen ViewModel.</summary>
    IInstallViewModel InstallView { get; }
    /// <summary>Gets the alert ViewModel driving the title bar's alarm box and sound.</summary>
    IAlertViewModel Alert { get; }
    /// <summary>Gets the export ViewModel driving the export screen.</summary>
    IExportViewModel Export { get; }
    /// <summary>Gets the import ViewModel driving the import screen.</summary>
    IImportViewModel Import { get; }
    /// <summary>
    /// Gets the staged send ViewModel driving the staged send screen. Unlike <see cref="Export"/>/<see cref="Import"/>/
    /// <see cref="AutoForward"/>, there is no command to navigate here directly - the screen only ever appears
    /// automatically, shown by an import whose format added staged sends, and is discarded the moment the user
    /// navigates elsewhere.
    /// </summary>
    IStagedSendViewModel StagedSend { get; }
    /// <summary>Gets the retrieve ViewModel driving the retrieve screen.</summary>
    IRetrieveViewModel Retrieve { get; }
    /// <summary>Gets a value indicating whether the title bar's RETRIEVE button is shown: only for a <see cref="UserRole.Client"/> on a network that has at least one server.</summary>
    bool CanRetrieve { get; }
    /// <summary>Gets the auto forward ViewModel driving the auto forward screen.</summary>
    IAutoForwardViewModel AutoForward { get; }
    /// <summary>Gets a value indicating whether the current user has access to at least one auto forward controller, and so should see the title bar's AUTO FORWARD button at all.</summary>
    bool HasAutoForwardAccess { get; }
    /// <summary>Gets the print manager ViewModel driving the print queue screen.</summary>
    IPrintManagerViewModel PrintManager { get; }
    /// <summary>Creates a new draft and displays it in the content area.</summary>
    IAsyncRelayCommand CreateDraftCommand { get; }
    /// <summary>Creates a new note and displays it in the content area.</summary>
    IAsyncRelayCommand CreateNoteCommand { get; }
    /// <summary>Displays the export screen in the content area, refreshing the available drive list first.</summary>
    IRelayCommand ShowExportCommand { get; }
    /// <summary>Displays the import screen in the content area, refreshing the available drive list first.</summary>
    IRelayCommand ShowImportCommand { get; }
    /// <summary>Displays the retrieve screen in the content area.</summary>
    IRelayCommand ShowRetrieveCommand { get; }
    /// <summary>Displays the auto forward screen in the content area, refreshing which controllers the current user has access to first.</summary>
    IRelayCommand ShowAutoForwardCommand { get; }
    /// <summary>Displays the print manager screen in the content area.</summary>
    IRelayCommand ShowPrintManagerCommand { get; }
    /// <summary>
    /// Re-reads the network configuration file and applies what changed: connections are brought down or opened as the file now defines them, and the
    /// role, security level and access shown in the UI are updated. A file that cannot be read is logged and leaves everything as it was.
    /// </summary>
    IRelayCommand RefreshCommand { get; }
    /// <summary>Restores the content area to its default (home) state, without disturbing any other ViewModel's state.</summary>
    IRelayCommand ShowHomeCommand { get; }
    /// <summary>Switches <see cref="IsServerMode"/>'s view to the connections table.</summary>
    IRelayCommand ShowConnectionsCommand { get; }
    /// <summary>Switches <see cref="IsServerMode"/>'s view to the activity log, selecting (or refreshing) the Activity folder.</summary>
    IAsyncRelayCommand ShowActivityCommand { get; }
    /// <summary>Adds the given entry to the print queue as a manual print.</summary>
    IRelayCommand<EntryItemViewModel> PrintEntryCommand { get; }
    /// <summary>Connects to the service, loads user info, and initializes either the main UI or the install screen.</summary>
    Task Initialize();
}

/// <summary>Root ViewModel for the main application window, coordinating folder, entry, and content area ViewModels.</summary>
internal sealed partial class MainViewModel : ObservableObject, IMainViewModel
{
    /// <summary>Initializes a new <see cref="MainViewModel"/> with all required engine and UI dependencies.</summary>
    /// <param name="connection">Service connection used for user and messaging operations.</param>
    /// <param name="db">LiteDB context for lazy initialization after install.</param>
    /// <param name="entryService">Entry CRUD service for messages, drafts, and notes.</param>
    /// <param name="folderBar">Folder tree ViewModel.</param>
    /// <param name="entryBar">Entry list ViewModel.</param>
    /// <param name="contentArea">Content area ViewModel.</param>
    /// <param name="installViewModel">Install screen ViewModel.</param>
    /// <param name="alert">Alert ViewModel driving the title bar's alarm box and sound.</param>
    /// <param name="export">Export ViewModel driving the export screen.</param>
    /// <param name="import">Import ViewModel driving the import screen.</param>
    /// <param name="stagedSend">Staged send ViewModel driving the staged send screen.</param>
    /// <param name="retrieve">Retrieve ViewModel driving the retrieve screen.</param>
    /// <param name="autoForward">Auto forward ViewModel driving the auto forward screen.</param>
    /// <param name="printManager">Print manager ViewModel driving the print queue screen.</param>
    /// <param name="help">Help ViewModel driving the help window opened from the title bar.</param>
    /// <param name="connectionStatus">Connection status ViewModel driving <see cref="IsServerMode"/>'s connections table and <see cref="IsClientMode"/>'s connection row.</param>
    /// <param name="currentUserProvider">Provides and accepts the current user name.</param>
    /// <param name="engineController">Provides the application display name, whether the UI should run in kiosk mode, alert settings, message composition settings, and the configured node role.</param>
    /// <param name="networkReload">Re-reads the network configuration file on request, for <see cref="RefreshCommand"/>.</param>
    /// <param name="loggerFactory">Factory for creating named loggers.</param>
    /// <param name="bodyDocumentFactory">Factory for creating the body document for new drafts.</param>
    public MainViewModel(
        IServiceConnection connection,
        ILiteDbContext db,
        IEntryService entryService,
        IFolderBarViewModel folderBar,
        IEntryBarViewModel entryBar,
        IContentAreaViewModel contentArea,
        IInstallViewModel installViewModel,
        IAlertViewModel alert,
        IExportViewModel export,
        IImportViewModel import,
        IStagedSendViewModel stagedSend,
        IRetrieveViewModel retrieve,
        IAutoForwardViewModel autoForward,
        IPrintManagerViewModel printManager,
        IHelpViewModel help,
        IConnectionStatusViewModel connectionStatus,
        ICurrentUserProvider currentUserProvider,
        IEngineController engineController,
        INetworkReloadService networkReload,
        ILoggerFactory loggerFactory,
        IBodyDocumentFactory bodyDocumentFactory)
    {
        this.networkReload = networkReload;
        this.connection = connection;
        this.db = db;
        this.entryService = entryService;
        this.folderBar = folderBar;
        this.entryBar = entryBar;
        this.contentArea = contentArea;
        this.installViewModel = installViewModel;
        this.alert = alert;
        this.export = export;
        this.import = import;
        this.stagedSend = stagedSend;
        this.retrieve = retrieve;
        this.autoForward = autoForward;
        this.printManager = printManager;
        Help = help;
        this.connectionStatus = connectionStatus;
        this.currentUserProvider = currentUserProvider;
        this.engineController = engineController;
        this.bodyDocumentFactory = bodyDocumentFactory;
        this.loggerFactory = loggerFactory;
        logger = loggerFactory.CreateLogger("APP");
        activityLogger = loggerFactory.CreateLogger("ACTIVITY");

        isKioskMode = engineController.IsKioskMode;
        appVersion = engineController.AppVersion;
        ApplyRole();
        networkReload.Reloaded += OnNetworkReloaded;
        WireEvents();
    }

    private readonly IServiceConnection connection;
    private readonly ILiteDbContext db;
    private readonly IEntryService entryService;
    private readonly IFolderBarViewModel folderBar;
    private readonly IEntryBarViewModel entryBar;
    private readonly IContentAreaViewModel contentArea;
    private readonly IInstallViewModel installViewModel;
    private readonly IAlertViewModel alert;
    private readonly IExportViewModel export;
    private readonly IImportViewModel import;
    private readonly IStagedSendViewModel stagedSend;
    private readonly IRetrieveViewModel retrieve;
    private readonly IAutoForwardViewModel autoForward;
    private readonly IPrintManagerViewModel printManager;
    private readonly IConnectionStatusViewModel connectionStatus;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly IEngineController engineController;
    private readonly INetworkReloadService networkReload;
    private readonly IBodyDocumentFactory bodyDocumentFactory;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger logger;
    private readonly ILogger activityLogger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMainLayout))]
    [NotifyPropertyChangedFor(nameof(ShowConnectionsTable))]
    [NotifyPropertyChangedFor(nameof(ShowServerActivityView))]
    private bool isInstallScreenVisible;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMainLayout))]
    [NotifyPropertyChangedFor(nameof(ShowConnectionsTable))]
    [NotifyPropertyChangedFor(nameof(ShowServerActivityView))]
    private bool isServerMode;
    [ObservableProperty] private bool isClientMode;
    [ObservableProperty] private bool canRetrieve;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConnectionsTable))]
    [NotifyPropertyChangedFor(nameof(ShowServerActivityView))]
    private bool isServerActivityViewActive;
    [ObservableProperty] private bool isKioskMode;
    [ObservableProperty] private string userName = string.Empty;
    [ObservableProperty] private string securityLevelName = string.Empty;
    [ObservableProperty] private string securityLevelColor = "#1565C0";
    [ObservableProperty] private string appVersion;
    [ObservableProperty] private bool hasAutoForwardAccess;

    /// <inheritdoc />
    public IConnectionStatusViewModel ConnectionStatus => connectionStatus;
    /// <inheritdoc />
    public bool ShowMainLayout => !IsServerMode && !IsInstallScreenVisible;
    /// <inheritdoc />
    public bool ShowConnectionsTable => IsServerMode && !IsInstallScreenVisible && !IsServerActivityViewActive;
    /// <inheritdoc />
    public bool ShowServerActivityView => IsServerMode && !IsInstallScreenVisible && IsServerActivityViewActive;
    /// <inheritdoc />
    public IFolderBarViewModel FolderBar => folderBar;
    /// <inheritdoc />
    public IEntryBarViewModel EntryBar => entryBar;
    /// <inheritdoc />
    public IContentAreaViewModel ContentArea => contentArea;
    /// <inheritdoc />
    public IInstallViewModel InstallView => installViewModel;
    /// <inheritdoc />
    public IAlertViewModel Alert => alert;
    /// <inheritdoc />
    public IExportViewModel Export => export;
    /// <inheritdoc />
    public IImportViewModel Import => import;
    /// <inheritdoc />
    public IStagedSendViewModel StagedSend => stagedSend;
    /// <inheritdoc />
    public IRetrieveViewModel Retrieve => retrieve;
    /// <inheritdoc />
    /// <inheritdoc />
    public IAutoForwardViewModel AutoForward => autoForward;
    /// <inheritdoc />
    public IPrintManagerViewModel PrintManager => printManager;
    /// <inheritdoc />
    public IHelpViewModel Help { get; }
    /// <inheritdoc />
    public string AppName => engineController.AppName;

    private bool isRevealing;

    private void WireEvents()
    {
        folderBar.FolderSelected += async folder =>
        {
            // While the export view is active and collecting entries ("Some" scope), browsing folders
            // refreshes the entry listing to pick more entries from without leaving the export view.
            if (!IsExportCollectingActive() && !isRevealing)
            {
                contentArea.ShowHome();
            }
            await entryBar.LoadFolder(folder);
        };

        folderBar.EntryMoved += async () =>
            await entryBar.Refresh();

        entryBar.EntriesSelected += async entries =>
        {
            if (IsExportCollectingActive())
            {
                foreach (EntryItemViewModel entry in entries)
                {
                    export.AddEntry(entry);
                }
            }
            else if (entries.Count == 1 && !IsShown(entries[0]))
            {
                await contentArea.ShowEntry(entries[0]);
            }
        };

        alert.OpenRequested += id => contentArea.ShowEntry(new EntryItemViewModel(id, string.Empty, EntryType.Message, DateTime.UtcNow));

        contentArea.EntryOpened += Reveal;

        entryBar.EntryDeleted += entry =>
        {
            bool isActiveContent = contentArea.ActiveContent switch
            {
                IMessageViewModel msg => msg.MessageId == entry.Id,
                IDraftViewModel draft => draft.Id == entry.Id,
                INoteViewModel note => note.Id == entry.Id,
                _ => false
            };
            if (isActiveContent)
            {
                contentArea.ShowHome();
            }
        };

        installViewModel.InstallSucceeded += async info =>
        {
            db.Initialize();
            currentUserProvider.UserName = info.Name;
            await ApplyUserInfo(info);
            await StartMainUi();
            logger.LogInformation("{AppName} started", engineController.AppName);
            IsInstallScreenVisible = false;
        };

        contentArea.DraftSent += HandleDraftSent;
        contentArea.EntryDeleted += HandleEntryDeleted;

        import.StagedSendsReady += () =>
        {
            ShowStagedSend();
            return Task.CompletedTask;
        };

        connection.DeliveryStatusChanged += evt => UiThread.Run(() => entryBar.UpdateEntryStatus(evt.MessageId, evt.OverallStatus));

        connection.MessageReceived += evt => UiThread.Run(() => HandleMessageReceived(evt));

        entryService.DraftUpdated += async entity =>
        {
            entryBar.SetPendingSelectId(entity.Id.ToString());
            FolderItemViewModel? draftsFolder = folderBar.RootFolders.FirstOrDefault(f => f.RootType == FolderType.Drafts);
            if (draftsFolder is null) { return; }
            if (folderBar.SelectedFolder?.Id == draftsFolder.Id)
            {
                await entryBar.Refresh();
            }
            else
            {
                folderBar.SelectFolderByType(FolderType.Drafts);
            }
        };

        contentArea.EntryTitleChanged += entryBar.UpdateTitle;

        entryService.DraftInserted += entity =>
        {
            entryBar.AddEntry(entity);
            return Task.CompletedTask;
        };

        entryService.NoteInserted += entity =>
        {
            entryBar.AddEntry(entity);
            return Task.CompletedTask;
        };

        entryService.DraftSavedQuietly += entity =>
        {
            entryBar.UpdateEntry(entity);
            return Task.CompletedTask;
        };

        entryService.NoteSavedQuietly += entity =>
        {
            entryBar.UpdateEntry(entity);
            return Task.CompletedTask;
        };

        entryService.NoteUpdated += async entity =>
        {
            entryBar.SetPendingSelectId(entity.Id.ToString());
            FolderItemViewModel? notesFolder = folderBar.RootFolders.FirstOrDefault(f => f.RootType == FolderType.Notes);
            if (notesFolder is null) { return; }
            if (folderBar.SelectedFolder?.Id == notesFolder.Id)
            {
                await entryBar.Refresh();
            }
            else
            {
                folderBar.SelectFolderByType(FolderType.Notes);
            }
        };
    }

    private async Task HandleMessageReceived(MessageReceivedEvent evt)
    {
        try
        {
            // A storage server's answer to a retrieval request can include messages this inbox already holds.
            if (await entryService.IncomingMessageExists(evt.MessageId)) { return; }

            MessageEntity entity = await entryService.StoreIncomingMessage(
                evt.MessageId, evt.FromUser, evt.Body,
                evt.Addresses.Select(a => new AddressData { UserName = a.UserName, Type = a.Type, Information = a.Information }).ToList(),
                evt.SentAt, evt.Priority, evt.Tag, engineController.GetSecurityLevelName(evt.SecurityLevel));

            FolderItemViewModel? inboxFolder = FindMessageRoot(FolderType.Inbox, evt.IsAlert);
            if (inboxFolder is not null && folderBar.SelectedFolder?.Id == inboxFolder.Id)
            {
                string timeText = entity.ReceivedAt.ToString("dd-MMM-yyyy HH:mm").ToUpperInvariant();
                string priorityText = engineController.NameOf(engineController.ResolvePriority(evt.Priority));
                string? tagText = engineController.TagsEnabled && !string.IsNullOrEmpty(evt.Tag) ? evt.Tag : null;
                string securityLevelName = engineController.GetSecurityLevelName(evt.SecurityLevel);
                string? securityLevelColor = engineController.SecurityLevels.IsRecognized(securityLevelName) ? engineController.SecurityLevels.GetColor(securityLevelName) : null;
                EntryItemViewModel item = new(entity.MessageId, evt.FromUser, EntryType.Message, entity.ReceivedAt,
                    secondaryText: evt.Body.FirstLine, priorityText: priorityText, tagText: tagText, timeText: timeText, securityLevelColorHex: securityLevelColor, isAlert: evt.IsAlert);
                item.OverallStatus = entity.ReadStatus;
                await entryBar.PrependEntry(item);
            }
        }
        catch (Exception ex)
        {
            activityLogger.LogError(ex, "Failed to store received message from {FromUser}", evt.FromUser);
        }
    }

    /// <inheritdoc />
    public async Task Initialize()
    {
        try
        {
            await connection.Connect();
            UserInfo? userInfo = await connection.GetUserInfo();

            if (userInfo is not null)
            {
                db.Initialize();
                currentUserProvider.UserName = userInfo.Name;
                await ApplyUserInfo(userInfo);
                await StartMainUi();
            }
            else
            {
                IsInstallScreenVisible = true;
            }
        }
        catch (Exception ex) { logger.LogError(ex, "Initialization failed"); }
    }

    private async Task StartMainUi()
    {
        await folderBar.Load();
    }

    private void ApplyRole()
    {
        IsServerMode = engineController.Role is UserRole.Server or UserRole.Relay;
        IsClientMode = engineController.Role == UserRole.Client;
        CanRetrieve = IsClientMode && engineController.StorageServers.Count > 0;
    }

    private void OnNetworkReloaded()
    {
        if (!string.IsNullOrEmpty(UserName)) { _ = ApplyUserInfo(engineController.GetUserInfo(UserName)); }
    }

    private Task ApplyUserInfo(UserInfo info)
    {
        UserName = info.Name;
        ApplyRole();
        string level = engineController.GetUserSecurityLevel(info.Name);
        SecurityLevelName = level;
        SecurityLevelColor = engineController.SecurityLevels.GetColor(level);
        HasAutoForwardAccess = engineController.AutoForwardControllers.Any(c => c.Users.Contains(info.Name, StringComparer.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    private async Task HandleEntryDeleted()
    {
        contentArea.ShowHome();
        await entryBar.Refresh();
    }

    private async Task HandleDraftSent(MessageEntity msg)
    {
        FolderItemViewModel? outboxFolder = FindMessageRoot(FolderType.Outbox, engineController.GetIsAlert(msg.Message));
        if (outboxFolder is null) { return; }

        entryBar.SetPendingSelectId(msg.MessageId);
        if (folderBar.SelectedFolder?.Id == outboxFolder.Id)
        {
            await entryBar.Refresh();
        }
        else
        {
            folderBar.SelectFolder(outboxFolder);
        }
    }

    private bool IsShown(EntryItemViewModel entry) => contentArea.ActiveContent switch
    {
        IMessageViewModel message => entry.EntryType == EntryType.Message && message.MessageId == entry.Id && message.IsOutbound == entry.IsOutboundMessage,
        IDraftViewModel draft => entry.EntryType == EntryType.Draft && draft.Id == entry.Id,
        INoteViewModel note => entry.EntryType == EntryType.Note && note.Id == entry.Id,
        _ => false
    };

    // Whatever is opened in the content area, however it got there, is shown in the listings as well: its folder is selected and it is selected in that folder's list.
    private async Task Reveal(EntryType type, string id, bool isOutbound)
    {
        if (entryBar.Entries?.Any(entry => entry.IsSelected && entry.Id == id && entry.EntryType == type) == true) { return; }

        EntryLocation? location = await entryService.Locate(id, type, isOutbound);
        if (location is null) { return; }

        FolderItemViewModel? folder = FindFolder(folderBar.RootFolders, location.FolderId);
        if (folder is { IsRootFolder: true, AlertView: not null }) { folder = FindMessageRoot(folder.RootType, location.IsAlert) ?? folder; }
        if (folder is null) { return; }

        entryBar.SetPendingSelectId(id);
        if (folderBar.SelectedFolder?.Id == folder.Id)
        {
            await entryBar.Refresh();
            return;
        }

        isRevealing = true;
        try { folderBar.SelectFolder(folder); }
        finally { isRevealing = false; }
    }

    private static FolderItemViewModel? FindFolder(IEnumerable<FolderItemViewModel> folders, string storageId)
    {
        foreach (FolderItemViewModel folder in folders)
        {
            if (folder.StorageId == storageId && folder.AlertView is not true) { return folder; }
            if (FindFolder(folder.Children, storageId) is { } found) { return found; }
        }

        return null;
    }

    // While alerts are kept apart the inbox and the outbox each come as two roots, one for alerts and one for the rest; otherwise there is one.
    private FolderItemViewModel? FindMessageRoot(FolderType type, bool isAlert)
        => folderBar.RootFolders.FirstOrDefault(folder => folder.RootType == type && folder.IsRootFolder && (folder.AlertView is null || folder.AlertView == isAlert));

    [RelayCommand]
    private async Task CreateDraft()
    {
        DraftEntity entity = await entryService.NewDraft();
        List<string> userNames = await connection.GetUserNames();
        string currentSecurityLevel = engineController.GetUserSecurityLevel(currentUserProvider.UserName ?? string.Empty);
        DraftViewModel vm = new(entity, entryService, connection, userNames, loggerFactory, engineController, bodyDocumentFactory.Create(), currentSecurityLevel: currentSecurityLevel, isNew: true);
        vm.Duplicated += contentArea.ShowDraft;
        vm.TitleChanged += title => entryBar.UpdateTitle(vm.Id, EntryType.Draft, title);
        vm.DraftSent += async (IDraftViewModel _, MessageEntity msg) =>
        {
            contentArea.ShowEntry(new MessageViewModel(msg, engineController));
            await HandleDraftSent(msg);
        };
        vm.Deleted += HandleEntryDeleted;
        OpenNew(vm, FolderType.Drafts);
    }

    private void OpenNew(object editor, FolderType folderType)
    {
        isRevealing = true;
        try
        {
            contentArea.ShowEntry(editor);
            entryBar.DeselectEntry();
            folderBar.SelectFolderByType(folderType);
        }
        finally
        {
            isRevealing = false;
        }
    }

    [RelayCommand]
    private async Task CreateNote()
    {
        NoteEntity entity = await entryService.NewNote();
        NoteViewModel vm = new(entity, entryService, engineController.CanDelete(FolderType.Notes), isNew: true);
        vm.Duplicated += contentArea.ShowNote;
        vm.TitleChanged += title => entryBar.UpdateTitle(vm.Id, EntryType.Note, title);
        vm.Deleted += HandleEntryDeleted;
        OpenNew(vm, FolderType.Notes);
    }

    [RelayCommand]
    private void ShowExport()
    {
        DeselectFolderAndEntry();
        export.RefreshDrivesCommand.Execute(null);
        contentArea.ShowEntry(export);
    }

    [RelayCommand]
    private void ShowImport()
    {
        DeselectFolderAndEntry();
        import.RefreshDrivesCommand.Execute(null);
        contentArea.ShowEntry(import);
    }

    private void ShowStagedSend()
    {
        DeselectFolderAndEntry();
        contentArea.ShowEntry(stagedSend);
    }

    [RelayCommand]
    private void Refresh()
    {
        try { networkReload.Reload(); }
        catch (Exception ex) { activityLogger.LogError(ex, "The network configuration could not be reloaded: {Message}", ex.Message); }
    }

    [RelayCommand]
    private void ShowRetrieve()
    {
        DeselectFolderAndEntry();
        contentArea.ShowEntry(retrieve);
    }

    [RelayCommand]
    private void ShowAutoForward()
    {
        DeselectFolderAndEntry();
        autoForward.RefreshCommand.Execute(null);
        contentArea.ShowEntry(autoForward);
    }

    [RelayCommand]
    private void ShowPrintManager()
    {
        DeselectFolderAndEntry();
        contentArea.ShowEntry(printManager);
    }

    [RelayCommand]
    private void PrintEntry(EntryItemViewModel entry) => printManager.EnqueueManual(entry);

    [RelayCommand]
    private void ShowHome() => contentArea.ShowHome();

    [RelayCommand]
    private void ShowConnections() => IsServerActivityViewActive = false;

    [RelayCommand]
    private async Task ShowActivity()
    {
        IsServerActivityViewActive = true;
        if (folderBar.SelectedFolder?.RootType == FolderType.Activity)
        {
            await entryBar.Refresh();
        }
        else
        {
            folderBar.SelectFolderByType(FolderType.Activity);
        }
    }

    private void DeselectFolderAndEntry()
    {
        folderBar.DeselectFolder();
        entryBar.DeselectEntry();
    }

    private bool IsExportCollectingActive()
        => ReferenceEquals(contentArea.ActiveContent, export) && export.IsCollectingEntries;
}
