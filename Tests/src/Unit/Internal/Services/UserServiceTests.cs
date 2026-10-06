namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Integration tests for <see cref="UserService"/> covering install, load, and state queries.</summary>
public sealed class UserServiceTests : IDisposable
{
    public UserServiceTests()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
        engineControllerMock.Setup(e => e.UserFilePath).Returns(Path.Combine(path, "User.json"));
    }

    private sealed class PathedController(string userFile, NetworkConfig network) : EngineController(EngineBuilder.Build(new TestEngineConfiguration()), new CurrentUserProvider(), network)
    {
        public override string UserFilePath => userFile;
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

    /// <summary>Verifies that Install returns the resolved user info for a valid user name.</summary>
    [Fact]
    public async Task InstallAsync_WithValidName_ReturnsUserInfo()
    {
        engineControllerMock.Setup(r => r.FindUserName("TS01")).Returns("TestUser");
        engineControllerMock.Setup(r => r.GetUserInfo("TestUser")).Returns(new UserInfo { Name = "TestUser" });

        UserService service = CreateService();
        UserInfo? result = await service.Install("TS01");

        Assert.NotNull(result);
        Assert.Equal("TestUser", result.Name);
    }

    /// <summary>Verifies that Install returns null when the user name is unknown.</summary>
    [Fact]
    public async Task InstallAsync_WithUnknownName_ReturnsNull()
    {
        engineControllerMock.Setup(r => r.FindUserName("INVALID")).Returns((string?)null);

        UserService service = CreateService();
        UserInfo? result = await service.Install("INVALID");

        Assert.Null(result);
    }

    /// <summary>Verifies that the service reports as installed after a successful Install call.</summary>
    [Fact]
    public async Task InstallAsync_WithValidName_MakesServiceInstalled()
    {
        engineControllerMock.Setup(r => r.FindUserName("MN01")).Returns("MyNode");
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
        engineControllerMock.Setup(r => r.FindUserName("RS01")).Returns("Restored");
        engineControllerMock.Setup(r => r.FindUserName("Restored")).Returns("Restored");
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
        engineControllerMock.Setup(r => r.FindUserName("OK01")).Returns("Ok");
        engineControllerMock.Setup(r => r.GetUserInfo("Ok")).Returns(new UserInfo { Name = "Ok" });
        engineControllerMock.Setup(r => r.FindUserName("BAD")).Returns((string?)null);
        UserService service = CreateService();
        List<string?> raised = [];
        service.Installed += () => raised.Add(service.GetCurrentUserInfo()?.Name);

        await service.Install("BAD");
        await service.Install("OK01");

        Assert.Equal(["Ok"], raised);
    }

    /// <summary>An install whose certificate is not good fails with the reason and installs nothing, so nothing is persisted either.</summary>
    [Fact]
    public async Task Install_CertificateProblem_ThrowsAndInstallsNothing()
    {
        engineControllerMock.Setup(r => r.FindUserName("ALICE")).Returns("ALICE");
        engineControllerMock.Setup(r => r.GetUserInfo("ALICE")).Returns(new UserInfo { Name = "ALICE" });
        engineControllerMock.Setup(r => r.GetCertificateProblem("ALICE")).Returns("no certificate");
        UserService service = CreateService();
        bool raised = false;
        service.Installed += () => raised = true;

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Install("ALICE"));

        Assert.Equal("no certificate", error.Message);
        Assert.Null(service.GetCurrentUserInfo());
        Assert.False(raised);
        Assert.False(File.Exists(engineControllerMock.Object.UserFilePath));
    }

    /// <summary>An installed user whose certificate has gone bad is uninstalled on startup: the state file is deleted and nobody is current, so the install screen is shown.</summary>
    [Fact]
    public async Task Load_InstalledUserWithBadCertificate_DeletesTheStateAndInstallsNobody()
    {
        engineControllerMock.Setup(r => r.FindUserName("ALICE")).Returns("ALICE");
        engineControllerMock.Setup(r => r.GetUserInfo("ALICE")).Returns(new UserInfo { Name = "ALICE" });
        await CreateService().Install("ALICE");
        Assert.True(File.Exists(engineControllerMock.Object.UserFilePath));
        engineControllerMock.Setup(r => r.GetCertificateProblem("ALICE")).Returns("signed by someone else");
        CurrentUserProvider current = new();
        UserService restarted = new(engineControllerMock.Object, current, LoggerFactory.Create(_ => { }));

        await restarted.Load();

        Assert.Null(restarted.GetCurrentUserInfo());
        Assert.Null(current.UserName);
        Assert.False(File.Exists(engineControllerMock.Object.UserFilePath));
    }

    /// <summary>An installed user whose certificate is still good is restored on startup.</summary>
    [Fact]
    public async Task Load_InstalledUserWithGoodCertificate_IsRestored()
    {
        engineControllerMock.Setup(r => r.FindUserName("ALICE")).Returns("ALICE");
        engineControllerMock.Setup(r => r.GetUserInfo("ALICE")).Returns(new UserInfo { Name = "ALICE" });
        await CreateService().Install("ALICE");
        CurrentUserProvider current = new();
        UserService restarted = new(engineControllerMock.Object, current, LoggerFactory.Create(_ => { }));

        await restarted.Load();

        Assert.Equal("ALICE", current.UserName);
        Assert.True(File.Exists(engineControllerMock.Object.UserFilePath));
    }

    /// <summary>A user named on the command line is checked like an installed one: it becomes the user only when the network lists it and its certificate is in order.</summary>
    [Fact]
    public async Task Load_CommandLineUser_PassesTheSameChecks()
    {
        engineControllerMock.Setup(r => r.FindUserName("alice")).Returns("ALICE");
        engineControllerMock.Setup(r => r.GetUserInfo("ALICE")).Returns(new UserInfo { Name = "ALICE" });
        engineControllerMock.SetupGet(r => r.DebugUserName).Returns("alice");
        CurrentUserProvider current = new();
        UserService good = new(engineControllerMock.Object, current, LoggerFactory.Create(_ => { }));
        await good.Load();
        Assert.Equal("ALICE", current.UserName);
        Assert.Equal("ALICE", good.GetCurrentUserInfo()?.Name);

        engineControllerMock.Setup(r => r.GetCertificateProblem("ALICE")).Returns("unsigned");
        CurrentUserProvider badCertificate = new();
        await new UserService(engineControllerMock.Object, badCertificate, LoggerFactory.Create(_ => { })).Load();
        Assert.Null(badCertificate.UserName);

        engineControllerMock.SetupGet(r => r.DebugUserName).Returns("nobody");
        CurrentUserProvider unknown = new();
        await new UserService(engineControllerMock.Object, unknown, LoggerFactory.Create(_ => { })).Load();
        Assert.Null(unknown.UserName);
        Assert.False(File.Exists(engineControllerMock.Object.UserFilePath));
    }

    /// <summary>With the real controller and real certificate files: a listed user with a good certificate installs and is remembered in User.json, one whose certificate is signed by another authority does not, and when the remembered user's certificate file goes the next start uninstalls them.</summary>
    [Fact]
    public async Task RealCertificates_InstallRememberAndUninstall()
    {
        string store = Path.Combine(Path.GetTempPath(), $"comlink-install-{Guid.NewGuid():N}");
        Directory.CreateDirectory(store);
        try
        {
            (Dictionary<string, X509Certificate2> identities, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("ALICE");
            (Dictionary<string, X509Certificate2> strangers, _) = TestMsmtCertificates.CreateNamed("BOB");
            File.WriteAllBytes(Path.Combine(store, "authority.cer"), authorities[0].Export(X509ContentType.Cert));
            File.WriteAllBytes(Path.Combine(store, "ALICE.pfx"), identities["ALICE"].Export(X509ContentType.Pfx));
            File.WriteAllBytes(Path.Combine(store, "BOB.pfx"), strangers["BOB"].Export(X509ContentType.Pfx));
            NetworkConfig network = new()
            {
                CertificateStore = store,
                AuthorityCertificate = Path.Combine(store, "authority.cer"),
                Users = { ["ALICE"] = new NetworkUserConfig(), ["BOB"] = new NetworkUserConfig() }
            };
            string userFile = Path.Combine(store, "app", "User.json");
            PathedController controller = new(userFile, network);

            UserService service = new(controller, new CurrentUserProvider(), LoggerFactory.Create(_ => { }));
            Assert.Null(await service.Install("NOBODY"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.Install("BOB"));
            Assert.False(File.Exists(userFile));

            Assert.Equal("ALICE", (await service.Install("alice"))?.Name);
            Assert.Contains("ALICE", File.ReadAllText(userFile));

            CurrentUserProvider restarted = new();
            await new UserService(controller, restarted, LoggerFactory.Create(_ => { })).Load();
            Assert.Equal("ALICE", restarted.UserName);

            File.Delete(Path.Combine(store, "ALICE.pfx"));
            CurrentUserProvider afterLoss = new();
            await new UserService(controller, afterLoss, LoggerFactory.Create(_ => { })).Load();
            Assert.Null(afterLoss.UserName);
            Assert.False(File.Exists(userFile));
        }
        finally
        {
            Directory.Delete(store, recursive: true);
        }
    }
}
