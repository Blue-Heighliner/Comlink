namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="MainViewModel"/>.</summary>
public sealed class MainViewModelTests
{
    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });

    private static UserInfo MakeUserInfo(string name = "ALPHA") => new()
    {
        Name = name,
        Code = "CODE1",
        EnvironmentTitle = "PROD",
        EnvironmentColor = "#FF0000"
    };

    /// <summary>Helper that assembles all mocks and builds a <see cref="MainViewModel"/>.</summary>
    private sealed class Setup
    {
        public Mock<IServiceConnection> Connection { get; } = new();
        public Mock<ILiteDbContext> Db { get; } = new();
        public Mock<IEntryService> EntryService { get; } = new();
        public Mock<IFolderBarViewModel> FolderBar { get; } = new();
        public Mock<IEntryBarViewModel> EntryBar { get; } = new();
        public Mock<IContentAreaViewModel> ContentArea { get; } = new();
        public Mock<IInstallViewModel> InstallView { get; } = new();
        public Mock<IAlertViewModel> Alert { get; } = new();
        public Mock<IExportViewModel> Export { get; } = new();
        public Mock<IImportViewModel> Import { get; } = new();
        public Mock<IPrintManagerViewModel> PrintManager { get; } = new();
        public Mock<ICurrentUserProvider> UserProvider { get; } = new();
        public Mock<TestEngineController> EngineController { get; } = new() { CallBase = true };
        public Mock<IBodyDocumentFactory> BodyDocumentFactory { get; } = new();

        public MainViewModel BuildVm()
        {
            EngineController.Setup(a => a.AppName).Returns("TestApp");
            FolderBar.Setup(f => f.RootFolders).Returns([]);
            BodyDocumentFactory.Setup(f => f.Create()).Returns(new StringBodyDocument());
            EngineController.Setup(p => p.Priorities).Returns([new MessagePriorityOption { Name = "Normal", Value = 0 }]);
            EngineController.Setup(a => a.AlertLabel).Returns("ALERT");
            EngineController.Setup(a => a.ComposeAlertsEnabled).Returns(true);
            EngineController.Setup(t => t.TagsEnabled).Returns(true);
            EngineController.Setup(t => t.TagLabel).Returns("Tag");
            EngineController.Setup(p => p.BlockedCombinations).Returns([]);

            return new MainViewModel(
                Connection.Object,
                Db.Object,
                EntryService.Object,
                FolderBar.Object,
                EntryBar.Object,
                ContentArea.Object,
                InstallView.Object,
                Alert.Object,
                Export.Object,
                Import.Object,
                PrintManager.Object,
                UserProvider.Object,
                EngineController.Object,
                noLogger,
                BodyDocumentFactory.Object);
        }
    }

    /// <summary>FolderBar, EntryBar, ContentArea, InstallView, and Alert expose the injected interfaces.</summary>
    [Fact]
    public void Properties_ExposeInjectedSubViewModels()
    {
        Setup s = new();
        MainViewModel vm = s.BuildVm();

        Assert.Same(s.FolderBar.Object, vm.FolderBar);
        Assert.Same(s.EntryBar.Object, vm.EntryBar);
        Assert.Same(s.ContentArea.Object, vm.ContentArea);
        Assert.Same(s.InstallView.Object, vm.InstallView);
        Assert.Same(s.Alert.Object, vm.Alert);
        Assert.Same(s.Export.Object, vm.Export);
        Assert.Same(s.Import.Object, vm.Import);
    }

    /// <summary>IsKioskMode reflects the value from IEngineController at construction time.</summary>
    [Fact]
    public void IsKioskMode_ReflectsProvider()
    {
        Setup s = new();
        s.EngineController.Setup(a => a.IsKioskMode).Returns(true);
        MainViewModel vm = s.BuildVm();

        Assert.True(vm.IsKioskMode);
    }

    /// <summary>Initialize connects, initializes the DB, applies user info, and loads the folder tree when a user is installed.</summary>
    [Fact]
    public async Task Initialize_UserInstalled_ConnectsAppliesUserInfoAndLoadsFolder()
    {
        Setup s = new();
        s.Connection.Setup(c => c.Connect(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        s.Connection.Setup(c => c.GetUserInfo(It.IsAny<CancellationToken>())).ReturnsAsync(MakeUserInfo("BETA"));
        s.FolderBar.Setup(f => f.Load()).Returns(Task.CompletedTask);
        MainViewModel vm = s.BuildVm();

        await vm.Initialize();

        s.Connection.Verify(c => c.Connect(It.IsAny<CancellationToken>()), Times.Once);
        s.Db.Verify(d => d.Initialize(), Times.Once);
        s.FolderBar.Verify(f => f.Load(), Times.Once);
        Assert.Equal("BETA", vm.UserName);
        Assert.Equal("PROD", vm.EnvironmentTitle);
        Assert.False(vm.IsInstallScreenVisible);
    }

    /// <summary>Initialize shows the install screen when no user is installed and does not load the folder bar.</summary>
    [Fact]
    public async Task Initialize_NoUser_ShowsInstallScreen()
    {
        Setup s = new();
        s.Connection.Setup(c => c.Connect(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        s.Connection.Setup(c => c.GetUserInfo(It.IsAny<CancellationToken>())).ReturnsAsync((UserInfo?)null);
        MainViewModel vm = s.BuildVm();

        await vm.Initialize();

        Assert.True(vm.IsInstallScreenVisible);
        s.FolderBar.Verify(f => f.Load(), Times.Never);
        s.Db.Verify(d => d.Initialize(), Times.Never);
    }

    /// <summary>Initialize does not propagate exceptions from the connection.</summary>
    [Fact]
    public async Task Initialize_ConnectionThrows_DoesNotPropagate()
    {
        Setup s = new();
        s.Connection.Setup(c => c.Connect(It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("offline"));
        MainViewModel vm = s.BuildVm();

        Exception? ex = await Record.ExceptionAsync(vm.Initialize);

        Assert.Null(ex);
    }

    /// <summary>When FolderSelected fires, ShowHome and LoadFolder are called on the content area and entry bar.</summary>
    [Fact]
    public void FolderSelected_CallsShowHomeAndLoadFolder()
    {
        Setup s = new();
        s.ContentArea.Setup(c => c.ShowHome());
        s.EntryBar.Setup(e => e.LoadFolder(It.IsAny<FolderItemViewModel>())).Returns(Task.CompletedTask);
        MainViewModel vm = s.BuildVm();
        FolderItemViewModel folder = new("f1", "Inbox", FolderType.Inbox, null);

        s.FolderBar.Raise(f => f.FolderSelected += null!, folder);

        s.ContentArea.Verify(c => c.ShowHome(), Times.Once);
        s.EntryBar.Verify(e => e.LoadFolder(folder), Times.Once);
    }

    /// <summary>
    /// While the export view is active and collecting entries ("Some" scope), selecting a folder still
    /// refreshes the entry listing, but does not navigate the content area away from the export view.
    /// </summary>
    [Fact]
    public void FolderSelected_ExportViewCollecting_LoadsFolderWithoutShowingHome()
    {
        Setup s = new();
        s.EntryBar.Setup(e => e.LoadFolder(It.IsAny<FolderItemViewModel>())).Returns(Task.CompletedTask);
        MainViewModel vm = s.BuildVm();
        s.ContentArea.Setup(c => c.ActiveContent).Returns(s.Export.Object);
        s.Export.Setup(e => e.IsCollectingEntries).Returns(true);
        FolderItemViewModel folder = new("f1", "Inbox", FolderType.Inbox, null);

        s.FolderBar.Raise(f => f.FolderSelected += null!, folder);

        s.ContentArea.Verify(c => c.ShowHome(), Times.Never);
        s.EntryBar.Verify(e => e.LoadFolder(folder), Times.Once);
    }

    /// <summary>
    /// While the export view is active but not collecting entries (e.g. All scope), selecting a folder
    /// navigates the content area back to Home as usual.
    /// </summary>
    [Fact]
    public void FolderSelected_ExportViewActiveButNotCollecting_ShowsHome()
    {
        Setup s = new();
        s.EntryBar.Setup(e => e.LoadFolder(It.IsAny<FolderItemViewModel>())).Returns(Task.CompletedTask);
        MainViewModel vm = s.BuildVm();
        s.ContentArea.Setup(c => c.ActiveContent).Returns(s.Export.Object);
        s.Export.Setup(e => e.IsCollectingEntries).Returns(false);
        FolderItemViewModel folder = new("f1", "Inbox", FolderType.Inbox, null);

        s.FolderBar.Raise(f => f.FolderSelected += null!, folder);

        s.ContentArea.Verify(c => c.ShowHome(), Times.Once);
    }

    /// <summary>When EntryMoved fires, the entry bar is refreshed.</summary>
    [Fact]
    public void EntryMoved_RefreshesEntryBar()
    {
        Setup s = new();
        s.EntryBar.Setup(e => e.Refresh()).Returns(Task.CompletedTask);
        MainViewModel vm = s.BuildVm();

        s.FolderBar.Raise(f => f.EntryMoved += null!);

        s.EntryBar.Verify(e => e.Refresh(), Times.Once);
    }

    /// <summary>When the export view is not active, selecting a single entry shows it in the content area as usual.</summary>
    [Fact]
    public void EntriesSelected_ExportViewNotActive_ShowsEntry()
    {
        Setup s = new();
        s.ContentArea.Setup(c => c.ActiveContent).Returns((object?)null);
        s.ContentArea.Setup(c => c.ShowEntry(It.IsAny<EntryItemViewModel>())).Returns(Task.CompletedTask);
        MainViewModel vm = s.BuildVm();
        EntryItemViewModel entry = new("E1", "Title", EntryType.Message, DateTime.UtcNow);

        s.EntryBar.Raise(e => e.EntriesSelected += null!, (IReadOnlyList<EntryItemViewModel>)[entry]);

        s.ContentArea.Verify(c => c.ShowEntry(entry), Times.Once);
        s.Export.Verify(e => e.AddEntry(It.IsAny<EntryItemViewModel>()), Times.Never);
    }

    /// <summary>
    /// When the export view is not active and multiple entries are selected at once (e.g. shift-range),
    /// nothing is shown, since the content area can only display one entry at a time.
    /// </summary>
    [Fact]
    public void EntriesSelected_ExportViewNotActiveMultipleEntries_ShowsNothing()
    {
        Setup s = new();
        s.ContentArea.Setup(c => c.ActiveContent).Returns((object?)null);
        MainViewModel vm = s.BuildVm();
        EntryItemViewModel entry1 = new("E1", "Title1", EntryType.Message, DateTime.UtcNow);
        EntryItemViewModel entry2 = new("E2", "Title2", EntryType.Message, DateTime.UtcNow);

        s.EntryBar.Raise(e => e.EntriesSelected += null!, (IReadOnlyList<EntryItemViewModel>)[entry1, entry2]);

        s.ContentArea.Verify(c => c.ShowEntry(It.IsAny<EntryItemViewModel>()), Times.Never);
    }

    /// <summary>
    /// When the export view is active and it is collecting entries, selecting an entry adds it to the
    /// export list instead of opening it.
    /// </summary>
    [Fact]
    public void EntriesSelected_ExportViewActiveAndCollecting_AddsEntryInsteadOfShowing()
    {
        Setup s = new();
        MainViewModel vm = s.BuildVm();
        s.ContentArea.Setup(c => c.ActiveContent).Returns(s.Export.Object);
        s.Export.Setup(e => e.IsCollectingEntries).Returns(true);
        EntryItemViewModel entry = new("E1", "Title", EntryType.Message, DateTime.UtcNow);

        s.EntryBar.Raise(e => e.EntriesSelected += null!, (IReadOnlyList<EntryItemViewModel>)[entry]);

        s.Export.Verify(e => e.AddEntry(entry), Times.Once);
        s.ContentArea.Verify(c => c.ShowEntry(It.IsAny<EntryItemViewModel>()), Times.Never);
    }

    /// <summary>
    /// When collecting entries and a shift-range/ctrl-click selects several entries at once, every one of
    /// them is added to the export list.
    /// </summary>
    [Fact]
    public void EntriesSelected_ExportViewCollectingMultipleEntries_AddsAllToExport()
    {
        Setup s = new();
        MainViewModel vm = s.BuildVm();
        s.ContentArea.Setup(c => c.ActiveContent).Returns(s.Export.Object);
        s.Export.Setup(e => e.IsCollectingEntries).Returns(true);
        EntryItemViewModel entry1 = new("E1", "Title1", EntryType.Message, DateTime.UtcNow);
        EntryItemViewModel entry2 = new("E2", "Title2", EntryType.Message, DateTime.UtcNow);
        EntryItemViewModel entry3 = new("E3", "Title3", EntryType.Message, DateTime.UtcNow);

        s.EntryBar.Raise(e => e.EntriesSelected += null!, (IReadOnlyList<EntryItemViewModel>)[entry1, entry2, entry3]);

        s.Export.Verify(e => e.AddEntry(entry1), Times.Once);
        s.Export.Verify(e => e.AddEntry(entry2), Times.Once);
        s.Export.Verify(e => e.AddEntry(entry3), Times.Once);
    }

    /// <summary>
    /// When the export view is active but not collecting entries (e.g. All scope, or an export is running),
    /// selecting an entry still shows it normally.
    /// </summary>
    [Fact]
    public void EntriesSelected_ExportViewActiveButNotCollecting_ShowsEntry()
    {
        Setup s = new();
        s.ContentArea.Setup(c => c.ShowEntry(It.IsAny<EntryItemViewModel>())).Returns(Task.CompletedTask);
        MainViewModel vm = s.BuildVm();
        s.ContentArea.Setup(c => c.ActiveContent).Returns(s.Export.Object);
        s.Export.Setup(e => e.IsCollectingEntries).Returns(false);
        EntryItemViewModel entry = new("E1", "Title", EntryType.Message, DateTime.UtcNow);

        s.EntryBar.Raise(e => e.EntriesSelected += null!, (IReadOnlyList<EntryItemViewModel>)[entry]);

        s.ContentArea.Verify(c => c.ShowEntry(entry), Times.Once);
        s.Export.Verify(e => e.AddEntry(It.IsAny<EntryItemViewModel>()), Times.Never);
    }

    /// <summary>ShowExportCommand refreshes the drive list and displays the export ViewModel in the content area.</summary>
    [Fact]
    public void ShowExportCommand_RefreshesDrivesAndShowsExportView()
    {
        Setup s = new();
        s.Export.Setup(e => e.RefreshDrivesCommand).Returns(new RelayCommand(() => { }));
        MainViewModel vm = s.BuildVm();

        vm.ShowExportCommand.Execute(null);

        s.Export.VerifyGet(e => e.RefreshDrivesCommand, Times.Once);
        s.ContentArea.Verify(c => c.ShowEntry((object)s.Export.Object), Times.Once);
    }

    /// <summary>ShowExportCommand deselects the currently selected folder and entry.</summary>
    [Fact]
    public void ShowExportCommand_DeselectsFolderAndEntry()
    {
        Setup s = new();
        s.Export.Setup(e => e.RefreshDrivesCommand).Returns(new RelayCommand(() => { }));
        MainViewModel vm = s.BuildVm();

        vm.ShowExportCommand.Execute(null);

        s.FolderBar.Verify(f => f.DeselectFolder(), Times.Once);
        s.EntryBar.Verify(e => e.DeselectEntry(), Times.Once);
    }

    /// <summary>ShowImportCommand refreshes the drive list and displays the import ViewModel in the content area.</summary>
    [Fact]
    public void ShowImportCommand_RefreshesDrivesAndShowsImportView()
    {
        Setup s = new();
        s.Import.Setup(i => i.RefreshDrivesCommand).Returns(new RelayCommand(() => { }));
        MainViewModel vm = s.BuildVm();

        vm.ShowImportCommand.Execute(null);

        s.Import.VerifyGet(i => i.RefreshDrivesCommand, Times.Once);
        s.ContentArea.Verify(c => c.ShowEntry((object)s.Import.Object), Times.Once);
    }

    /// <summary>ShowImportCommand deselects the currently selected folder and entry.</summary>
    [Fact]
    public void ShowImportCommand_DeselectsFolderAndEntry()
    {
        Setup s = new();
        s.Import.Setup(i => i.RefreshDrivesCommand).Returns(new RelayCommand(() => { }));
        MainViewModel vm = s.BuildVm();

        vm.ShowImportCommand.Execute(null);

        s.FolderBar.Verify(f => f.DeselectFolder(), Times.Once);
        s.EntryBar.Verify(e => e.DeselectEntry(), Times.Once);
    }

    /// <summary>ShowPrintManagerCommand displays the print manager ViewModel in the content area.</summary>
    [Fact]
    public void ShowPrintManagerCommand_ShowsPrintManagerView()
    {
        Setup s = new();
        MainViewModel vm = s.BuildVm();

        vm.ShowPrintManagerCommand.Execute(null);

        s.ContentArea.Verify(c => c.ShowEntry((object)s.PrintManager.Object), Times.Once);
    }

    /// <summary>ShowPrintManagerCommand deselects the currently selected folder and entry.</summary>
    [Fact]
    public void ShowPrintManagerCommand_DeselectsFolderAndEntry()
    {
        Setup s = new();
        MainViewModel vm = s.BuildVm();

        vm.ShowPrintManagerCommand.Execute(null);

        s.FolderBar.Verify(f => f.DeselectFolder(), Times.Once);
        s.EntryBar.Verify(e => e.DeselectEntry(), Times.Once);
    }

    /// <summary>PrintEntryCommand adds the given entry to the print manager's queue as a manual print.</summary>
    [Fact]
    public void PrintEntryCommand_EnqueuesEntryAsManual()
    {
        Setup s = new();
        MainViewModel vm = s.BuildVm();
        EntryItemViewModel entry = new("N1", "My Note", EntryType.Note, DateTime.UtcNow);

        vm.PrintEntryCommand.Execute(entry);

        s.PrintManager.Verify(p => p.EnqueueManual(entry), Times.Once);
    }

    /// <summary>ShowHomeCommand restores the content area to its default (home) state.</summary>
    [Fact]
    public void ShowHomeCommand_ShowsHome()
    {
        Setup s = new();
        MainViewModel vm = s.BuildVm();

        vm.ShowHomeCommand.Execute(null);

        s.ContentArea.Verify(c => c.ShowHome(), Times.Once);
    }

    /// <summary>
    /// ShowHomeCommand (e.g. the close button in the export/import view) does not disturb the export or
    /// import ViewModel's own state — they remain untouched, as if the user had simply navigated away.
    /// </summary>
    [Fact]
    public void ShowHomeCommand_DoesNotResetExportOrImportState()
    {
        Setup s = new();
        MainViewModel vm = s.BuildVm();

        vm.ShowHomeCommand.Execute(null);

        s.Export.VerifyGet(e => e.RefreshDrivesCommand, Times.Never);
        s.Import.VerifyGet(i => i.RefreshDrivesCommand, Times.Never);
        s.FolderBar.Verify(f => f.DeselectFolder(), Times.Never);
        s.EntryBar.Verify(e => e.DeselectEntry(), Times.Never);
    }
}
