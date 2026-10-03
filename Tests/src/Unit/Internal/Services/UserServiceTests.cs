namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Integration tests for <see cref="UserService"/> covering install, load, and state queries.</summary>
public sealed class UserServiceTests : IDisposable
{
    public UserServiceTests()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
        engineControllerMock.Setup(e => e.StatePath).Returns(Path.Combine(path, "State.json"));
    }

    private readonly string appName = Guid.NewGuid().ToString();
    private readonly Mock<IEngineController> engineControllerMock = new();

    private UserService CreateService()
        => new(engineControllerMock.Object, new CurrentUserProvider(), LoggerFactory.Create(_ => { }));

    /// <summary>Verifies that GetCurrentUserInfo returns null when the user has not been installed.</summary>
    [Fact]
    public void GetCurrentUserInfo_WhenNotInstalled_ReturnsNull()
    {
        UserService service = CreateService();
        Assert.Null(service.GetCurrentUserInfo());
    }

    /// <summary>Verifies that Install returns the resolved user info for a valid user code.</summary>
    [Fact]
    public async Task InstallAsync_WithValidCode_ReturnsUserInfo()
    {
        engineControllerMock.Setup(r => r.ResolveUserName("TS01")).Returns("TestUser");
        engineControllerMock.Setup(r => r.GetUserInfo("TestUser")).Returns(new UserInfo { Name = "TestUser" });

        UserService service = CreateService();
        UserInfo? result = await service.Install("TS01");

        Assert.NotNull(result);
        Assert.Equal("TestUser", result.Name);
    }

    /// <summary>Verifies that Install returns null when the user code is unrecognized.</summary>
    [Fact]
    public async Task InstallAsync_WithInvalidCode_ReturnsNull()
    {
        engineControllerMock.Setup(r => r.ResolveUserName("INVALID")).Returns((string?)null);

        UserService service = CreateService();
        UserInfo? result = await service.Install("INVALID");

        Assert.Null(result);
    }

    /// <summary>Verifies that the service reports as installed after a successful Install call.</summary>
    [Fact]
    public async Task InstallAsync_WithValidCode_MakesServiceInstalled()
    {
        engineControllerMock.Setup(r => r.ResolveUserName("MN01")).Returns("MyNode");
        engineControllerMock.Setup(r => r.GetUserInfo("MyNode")).Returns(new UserInfo { Name = "MyNode" });

        UserService service = CreateService();
        await service.Install("MN01");

        UserInfo? result = service.GetCurrentUserInfo();
        Assert.NotNull(result);
        Assert.Equal("MyNode", result.Name);
    }

    /// <summary>Verifies that Load restores previously persisted user state from disk.</summary>
    [Fact]
    public async Task LoadAsync_WithExistingStateFile_RestoresState()
    {
        engineControllerMock.Setup(r => r.ResolveUserName("RS01")).Returns("Restored");
        engineControllerMock.Setup(r => r.GetUserInfo("Restored")).Returns(new UserInfo { Name = "Restored" });

        UserService service = CreateService();
        await service.Install("RS01");

        UserService service2 = CreateService();
        await service2.Load();

        UserInfo? result = service2.GetCurrentUserInfo();
        Assert.NotNull(result);
        Assert.Equal("Restored", result.Name);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string dir = Path.Combine(appData, appName);
        if (Directory.Exists(dir)) { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>A successful Install raises Installed once the user is current; a failed one does not.</summary>
    [Fact]
    public async Task Install_RaisesInstalledOnlyOnSuccess()
    {
        engineControllerMock.Setup(r => r.ResolveUserName("OK01")).Returns("Ok");
        engineControllerMock.Setup(r => r.GetUserInfo("Ok")).Returns(new UserInfo { Name = "Ok" });
        engineControllerMock.Setup(r => r.ResolveUserName("BAD")).Returns((string?)null);
        UserService service = CreateService();
        List<string?> raised = [];
        service.Installed += () => raised.Add(service.GetCurrentUserInfo()?.Name);

        await service.Install("BAD");
        await service.Install("OK01");

        Assert.Equal(["Ok"], raised);
    }
}
