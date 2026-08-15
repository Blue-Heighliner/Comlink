namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>ViewModel interface for the main application window.</summary>
public interface IMainViewModel
{
    /// <summary>Gets or sets a value indicating whether the install screen is visible instead of the main UI.</summary>
    bool IsInstallScreenVisible { get; set; }
    /// <summary>Gets or sets a value indicating whether the UI is running in kiosk mode.</summary>
    bool IsKioskMode { get; set; }
    /// <summary>Gets or sets the local user name displayed in the title bar.</summary>
    string UserName { get; set; }
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
    /// <summary>Gets the alert ViewModel driving the title bar's alarm box and sound.</summary>
    IAlertViewModel Alert { get; }
    /// <summary>Gets the export ViewModel driving the export screen.</summary>
    IExportViewModel Export { get; }
    /// <summary>Gets the import ViewModel driving the import screen.</summary>
    IImportViewModel Import { get; }
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
    /// <summary>Displays the print manager screen in the content area.</summary>
    IRelayCommand ShowPrintManagerCommand { get; }
    /// <summary>Restores the content area to its default (home) state, without disturbing any other ViewModel's state.</summary>
    IRelayCommand ShowHomeCommand { get; }
    /// <summary>Adds the given entry to the print queue as a manual print.</summary>
    IRelayCommand<EntryItemViewModel> PrintEntryCommand { get; }
    /// <summary>Connects to the service, loads user info, and initializes either the main UI or the install screen.</summary>
    Task Initialize();
}

/// <summary>Root ViewModel for the main application window, coordinating folder, entry, and content area ViewModels.</summary>
public sealed partial class MainViewModel : ObservableObject, IMainViewModel
{
    private static string GetAppVersion()
    {
        Version? version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
        return version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

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
    /// <param name="printManager">Print manager ViewModel driving the print queue screen.</param>
    /// <param name="currentUserProvider">Provides and accepts the current user name.</param>
    /// <param name="engineController">Provides the application display name, whether the UI should run in kiosk mode, alert settings, and message composition settings.</param>
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
        IPrintManagerViewModel printManager,
        ICurrentUserProvider currentUserProvider,
        IEngineController engineController,
        ILoggerFactory loggerFactory,
        IBodyDocumentFactory bodyDocumentFactory)
    {
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
        this.printManager = printManager;
        this.currentUserProvider = currentUserProvider;
        this.engineController = engineController;
        this.bodyDocumentFactory = bodyDocumentFactory;
        this.loggerFactory = loggerFactory;
        logger = loggerFactory.CreateLogger("APP");
        activityLogger = loggerFactory.CreateLogger("ACTIVITY");

        isKioskMode = engineController.IsKioskMode;
        appVersion = GetAppVersion();
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
    private readonly IPrintManagerViewModel printManager;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly IEngineController engineController;
    private readonly IBodyDocumentFactory bodyDocumentFactory;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger logger;
    private readonly ILogger activityLogger;

    [ObservableProperty] private bool isInstallScreenVisible;
    [ObservableProperty] private bool isKioskMode;
    [ObservableProperty] private string userName = string.Empty;
    [ObservableProperty] private string environmentTitle = string.Empty;
    [ObservableProperty] private string environmentColor = "#1565C0";
    [ObservableProperty] private string appVersion;

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
    public IPrintManagerViewModel PrintManager => printManager;

    private void WireEvents()
    {
        folderBar.FolderSelected += async folder =>
        {
            // While the export view is active and collecting entries ("Some" scope), browsing folders
            // refreshes the entry listing to pick more entries from without leaving the export view.
            if (!IsExportCollectingActive())
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
            else if (entries.Count == 1)
            {
                await contentArea.ShowEntry(entries[0]);
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

        connection.DeliveryStatusChanged += async evt =>
        {
            await entryBar.UpdateEntryStatus(evt.MessageId, evt.OverallStatus);
        };

        connection.MessageReceived += async evt =>
        {
            try
            {
                MessageEntity entity = await entryService.StoreIncomingMessage(
                    evt.MessageId, evt.FromUser, evt.Subject, evt.Body,
                    evt.Addresses.Select(a => new Data.Entities.AddressData { UserName = a.UserName, Type = a.Type }).ToList(),
                    evt.SentAt, evt.IsAlert, evt.Priority, evt.Tag);

                FolderItemViewModel? inboxFolder = folderBar.RootFolders.FirstOrDefault(f => f.RootType == FolderType.Inbox);
                if (inboxFolder is not null && folderBar.SelectedFolder?.Id == inboxFolder.Id)
                {
                    string timeText = entity.ReceivedAt.ToString("dd-MMM-yyyy HH:mm").ToUpperInvariant();
                    string priorityText = engineController.Priorities.GetLabel(evt.Priority);
                    string? tagText = engineController.TagsEnabled && !string.IsNullOrEmpty(evt.Tag) ? evt.Tag : null;
                    EntryItemViewModel item = new(entity.MessageId, evt.FromUser, EntryType.Message, entity.ReceivedAt,
                        secondaryText: evt.Subject, priorityText: priorityText, tagText: tagText, timeText: timeText);
                    item.OverallStatus = entity.ReadStatus;
                    await entryBar.PrependEntry(item);
                }
            }
            catch (Exception ex)
            {
                activityLogger.LogError(ex, "Failed to store received message from {FromUser}", evt.FromUser);
            }
        };

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

    private Task ApplyUserInfo(UserInfo info)
    {
        UserName = info.Name;
        EnvironmentTitle = info.EnvironmentTitle;
        EnvironmentColor = info.EnvironmentColor;
        return Task.CompletedTask;
    }

    private async Task HandleDraftSent(MessageEntity msg)
    {
        FolderItemViewModel? outboxFolder = folderBar.RootFolders.FirstOrDefault(f => f.RootType == FolderType.Outbox);
        if (outboxFolder is null) { return; }

        entryBar.SetPendingSelectId(msg.MessageId);
        if (folderBar.SelectedFolder?.Id == outboxFolder.Id)
        {
            await entryBar.Refresh();
        }
        else
        {
            folderBar.SelectFolderByType(FolderType.Outbox);
        }
    }

    [RelayCommand]
    private async Task CreateDraft()
    {
        DraftEntity entity = await entryService.CreateDraft();
        List<string> userNames = await connection.GetUserNames();
        Entries.DraftViewModel vm = new(entity, entryService, connection, userNames, loggerFactory, engineController, bodyDocumentFactory.Create());
        vm.DraftSent += async (IDraftViewModel _, MessageEntity msg) =>
        {
            contentArea.ShowEntry(new Entries.MessageViewModel(msg, engineController));
            await HandleDraftSent(msg);
        };
        contentArea.ShowEntry(vm);
    }

    [RelayCommand]
    private async Task CreateNote()
    {
        NoteEntity entity = await entryService.CreateNote();
        Entries.NoteViewModel vm = new(entity, entryService);
        contentArea.ShowEntry(vm);
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

    private void DeselectFolderAndEntry()
    {
        folderBar.DeselectFolder();
        entryBar.DeselectEntry();
    }

    private bool IsExportCollectingActive()
        => ReferenceEquals(contentArea.ActiveContent, export) && export.IsCollectingEntries;
}
