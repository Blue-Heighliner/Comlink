namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="MainViewModel"/>.</summary>
public sealed class MainViewModelTests
{
    private static readonly ILoggerFactory NoLogger = LoggerFactory.Create(_ => { });

    private static SiteInfo MakeSiteInfo(string name = "ALPHA") => new()
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
        public Mock<ICurrentSiteProvider> SiteProvider { get; } = new();
        public Mock<IAppNameProvider> AppName { get; } = new();
        public Mock<IKioskModeProvider> KioskMode { get; } = new();
        public Mock<IBodyDocumentFactory> BodyDocumentFactory { get; } = new();
        public IMessageFormat MessageFormat { get; } = new TestMessageFormat();

        public MainViewModel BuildVm()
        {
            AppName.Setup(a => a.AppName).Returns("TestApp");
            FolderBar.Setup(f => f.RootFolders).Returns([]);
            BodyDocumentFactory.Setup(f => f.Create()).Returns(new StringBodyDocument());

            return new MainViewModel(
                Connection.Object,
                Db.Object,
                EntryService.Object,
                FolderBar.Object,
                EntryBar.Object,
                ContentArea.Object,
                InstallView.Object,
                SiteProvider.Object,
                AppName.Object,
                KioskMode.Object,
                NoLogger,
                BodyDocumentFactory.Object,
                MessageFormat);
        }
    }

    // ── Sub-VM properties ─────────────────────────────────────────────────────

    /// <summary>FolderBar, EntryBar, ContentArea, and InstallView expose the injected interfaces.</summary>
    [Fact]
    public void Properties_ExposeInjectedSubViewModels()
    {
        Setup s = new();
        MainViewModel vm = s.BuildVm();

        Assert.Same(s.FolderBar.Object, vm.FolderBar);
        Assert.Same(s.EntryBar.Object, vm.EntryBar);
        Assert.Same(s.ContentArea.Object, vm.ContentArea);
        Assert.Same(s.InstallView.Object, vm.InstallView);
    }

    // ── KioskMode ─────────────────────────────────────────────────────────────

    /// <summary>IsKioskMode reflects the value from IKioskModeProvider at construction time.</summary>
    [Fact]
    public void IsKioskMode_ReflectsProvider()
    {
        Setup s = new();
        s.KioskMode.Setup(k => k.IsKioskMode).Returns(true);
        MainViewModel vm = s.BuildVm();

        Assert.True(vm.IsKioskMode);
    }

    // ── Initialize – site installed ───────────────────────────────────────────

    /// <summary>Initialize connects, initializes the DB, applies site info, and loads the folder tree when a site is installed.</summary>
    [Fact]
    public async Task Initialize_SiteInstalled_ConnectsAppliesSiteInfoAndLoadsFolder()
    {
        Setup s = new();
        s.Connection.Setup(c => c.Connect(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        s.Connection.Setup(c => c.GetSiteInfo(It.IsAny<CancellationToken>())).ReturnsAsync(MakeSiteInfo("BETA"));
        s.FolderBar.Setup(f => f.Load()).Returns(Task.CompletedTask);
        MainViewModel vm = s.BuildVm();

        await vm.Initialize();

        s.Connection.Verify(c => c.Connect(It.IsAny<CancellationToken>()), Times.Once);
        s.Db.Verify(d => d.Initialize(), Times.Once);
        s.FolderBar.Verify(f => f.Load(), Times.Once);
        Assert.Equal("BETA", vm.SiteName);
        Assert.Equal("PROD", vm.EnvironmentTitle);
        Assert.False(vm.IsInstallScreenVisible);
    }

    // ── Initialize – not installed ────────────────────────────────────────────

    /// <summary>Initialize shows the install screen when no site is installed and does not load the folder bar.</summary>
    [Fact]
    public async Task Initialize_NoSite_ShowsInstallScreen()
    {
        Setup s = new();
        s.Connection.Setup(c => c.Connect(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        s.Connection.Setup(c => c.GetSiteInfo(It.IsAny<CancellationToken>())).ReturnsAsync((SiteInfo?)null);
        MainViewModel vm = s.BuildVm();

        await vm.Initialize();

        Assert.True(vm.IsInstallScreenVisible);
        s.FolderBar.Verify(f => f.Load(), Times.Never);
        s.Db.Verify(d => d.Initialize(), Times.Never);
    }

    // ── Initialize – connection error ─────────────────────────────────────────

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

    // ── FolderSelected event wiring ───────────────────────────────────────────

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

    // ── EntryMoved event wiring ───────────────────────────────────────────────

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
}
