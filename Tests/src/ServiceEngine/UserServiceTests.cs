namespace BlueHeighliner.Comlink.Tests.ServiceEngine;

/// <summary>Integration tests for <see cref="UserService"/> covering install, load, and state queries.</summary>
public sealed class UserServiceTests : IDisposable
{
    private readonly string _appName = Guid.NewGuid().ToString();
    private readonly Mock<IUserIdentity> _resolverMock = new();
    private UserService CreateService() =>
        new(_resolverMock.Object, new TestAppDataPathProvider(_appName), new BlueHeighliner.Comlink.Engine.Control.CurrentUserProvider(), LoggerFactory.Create(_ => { }));

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
        UserInfo expected = new()
        {
            Name = "TestUser",
            Code = "TS01",
            EnvironmentTitle = "Test",
            EnvironmentColor = "#FF0000"
        };
        _resolverMock.Setup(r => r.ResolveCode("TS01", default)).ReturnsAsync(expected);

        UserService service = CreateService();
        UserInfo? result = await service.Install("TS01");

        Assert.NotNull(result);
        Assert.Equal("TestUser", result.Name);
        Assert.Equal("TS01", result.Code);
    }

    /// <summary>Verifies that Install returns null when the user code is unrecognized.</summary>
    [Fact]
    public async Task InstallAsync_WithInvalidCode_ReturnsNull()
    {
        _resolverMock.Setup(r => r.ResolveCode("INVALID", default)).ReturnsAsync((UserInfo?)null);

        UserService service = CreateService();
        UserInfo? result = await service.Install("INVALID");

        Assert.Null(result);
    }

    /// <summary>Verifies that the service reports as installed after a successful Install call.</summary>
    [Fact]
    public async Task InstallAsync_WithValidCode_MakesServiceInstalled()
    {
        UserInfo userInfo = new()
        {
            Name = "MyNode",
            Code = "MN01",
            EnvironmentTitle = "Prod",
            EnvironmentColor = "#00FF00"
        };
        _resolverMock.Setup(r => r.ResolveCode("MN01", default)).ReturnsAsync(userInfo);

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
        UserInfo userInfo = new()
        {
            Name = "Restored",
            Code = "RS01",
            EnvironmentTitle = "QA",
            EnvironmentColor = "#0000FF"
        };
        _resolverMock.Setup(r => r.ResolveCode("RS01", default)).ReturnsAsync(userInfo);

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
        string dir = Path.Combine(appData, _appName);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }
}
