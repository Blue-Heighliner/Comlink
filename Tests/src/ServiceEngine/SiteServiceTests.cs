namespace BlueHeighliner.Comlink.Tests.ServiceEngine;

/// <summary>Integration tests for <see cref="SiteService"/> covering install, load, and state queries.</summary>
public sealed class SiteServiceTests : IDisposable
{
    private readonly string _appName = Guid.NewGuid().ToString();
    private readonly Mock<ISiteCodeResolver> _resolverMock = new();
    private SiteService CreateService() =>
        new(_resolverMock.Object, [], new TestAppDataPathProvider(_appName), new BlueHeighliner.Comlink.Engine.Control.CurrentSiteProvider(), LoggerFactory.Create(_ => { }));

    /// <summary>Verifies that GetCurrentSiteInfo returns null when the site has not been installed.</summary>
    [Fact]
    public void GetCurrentSiteInfo_WhenNotInstalled_ReturnsNull()
    {
        SiteService service = CreateService();
        Assert.Null(service.GetCurrentSiteInfo());
    }

    /// <summary>Verifies that Install returns the resolved site info for a valid site code.</summary>
    [Fact]
    public async Task InstallAsync_WithValidCode_ReturnsSiteInfo()
    {
        SiteInfo expected = new()
        {
            Name = "TestSite",
            Code = "TS01",
            EnvironmentTitle = "Test",
            EnvironmentColor = "#FF0000"
        };
        _resolverMock.Setup(r => r.Resolve("TS01", default)).ReturnsAsync(expected);

        SiteService service = CreateService();
        SiteInfo? result = await service.Install("TS01");

        Assert.NotNull(result);
        Assert.Equal("TestSite", result.Name);
        Assert.Equal("TS01", result.Code);
    }

    /// <summary>Verifies that Install returns null when the site code is unrecognized.</summary>
    [Fact]
    public async Task InstallAsync_WithInvalidCode_ReturnsNull()
    {
        _resolverMock.Setup(r => r.Resolve("INVALID", default)).ReturnsAsync((SiteInfo?)null);

        SiteService service = CreateService();
        SiteInfo? result = await service.Install("INVALID");

        Assert.Null(result);
    }

    /// <summary>Verifies that the service reports as installed after a successful Install call.</summary>
    [Fact]
    public async Task InstallAsync_WithValidCode_MakesServiceInstalled()
    {
        SiteInfo siteInfo = new()
        {
            Name = "MyNode",
            Code = "MN01",
            EnvironmentTitle = "Prod",
            EnvironmentColor = "#00FF00"
        };
        _resolverMock.Setup(r => r.Resolve("MN01", default)).ReturnsAsync(siteInfo);

        SiteService service = CreateService();
        await service.Install("MN01");

        SiteInfo? result = service.GetCurrentSiteInfo();
        Assert.NotNull(result);
        Assert.Equal("MyNode", result.Name);
    }

    /// <summary>Verifies that Load restores previously persisted site state from disk.</summary>
    [Fact]
    public async Task LoadAsync_WithExistingStateFile_RestoresState()
    {
        SiteInfo siteInfo = new()
        {
            Name = "Restored",
            Code = "RS01",
            EnvironmentTitle = "QA",
            EnvironmentColor = "#0000FF"
        };
        _resolverMock.Setup(r => r.Resolve("RS01", default)).ReturnsAsync(siteInfo);

        SiteService service = CreateService();
        await service.Install("RS01");

        SiteService service2 = CreateService();
        await service2.Load();

        SiteInfo? result = service2.GetCurrentSiteInfo();
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
