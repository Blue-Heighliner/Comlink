namespace BlueHeighliner.Comlink.Tests.Control;

/// <summary>Unit tests for the default control-interface implementations in <see cref="Engine.Control"/>.</summary>
public sealed class ControlProviderTests
{
    // ── AppDataPathProvider ───────────────────────────────────────────────────

    private static string SystemAppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    /// <summary>Null DataFolder produces AppData\AppName.</summary>
    [Fact]
    public void AppDataPathProvider_NullDataFolder_UsesAppDataAppName()
    {
        Mock<IAppNameProvider> name = new();
        name.Setup(n => n.AppName).Returns("TestApp");
        AppDataPathProvider provider = new(name.Object, new EngineConfig());

        Assert.Equal(Path.Combine(SystemAppData, "TestApp"), provider.AppDataPath);
    }

    /// <summary>DataFolder starting with '@' is treated as relative to the default AppData\AppName directory.</summary>
    [Fact]
    public void AppDataPathProvider_AtPrefix_IsRelativeToDefault()
    {
        Mock<IAppNameProvider> name = new();
        name.Setup(n => n.AppName).Returns("MyApp");
        AppDataPathProvider provider = new(name.Object, new EngineConfig { DataFolder = "@test/sub" });

        string expected = Path.Combine(SystemAppData, "MyApp", "test", "sub");
        Assert.Equal(expected, provider.AppDataPath);
    }

    /// <summary>An absolute DataFolder path is used verbatim.</summary>
    [Fact]
    public void AppDataPathProvider_AbsolutePath_UsedDirectly()
    {
        Mock<IAppNameProvider> name = new();
        name.Setup(n => n.AppName).Returns("MyApp");
        string absolute = "/tmp/custom-data";
        AppDataPathProvider provider = new(name.Object, new EngineConfig { DataFolder = absolute });

        Assert.Equal(absolute, provider.AppDataPath);
    }

    // ── DebugUserOverride ─────────────────────────────────────────────────────

    /// <summary>UserName returns the value from config.</summary>
    [Fact]
    public void DebugUserOverride_ReturnsUserNameFromConfig()
    {
        DebugUserOverride provider = new(new EngineConfig { UserName = "ALPHA" });
        Assert.Equal("ALPHA", provider.UserName);
    }

    /// <summary>UserName is null when config has no override.</summary>
    [Fact]
    public void DebugUserOverride_NullWhenNotConfigured()
    {
        DebugUserOverride provider = new(new EngineConfig());
        Assert.Null(provider.UserName);
    }

    // ── KioskModeProvider ─────────────────────────────────────────────────────

    /// <summary>IsKioskMode is always false.</summary>
    [Fact]
    public void KioskModeProvider_IsAlwaysFalse()
    {
        KioskModeProvider provider = new();
        Assert.False(provider.IsKioskMode);
    }

    // ── HomeContentProvider ───────────────────────────────────────────────────

    /// <summary>GetHomeText returns "HOME".</summary>
    [Fact]
    public void HomeContentProvider_ReturnsHOME()
    {
        HomeContentProvider provider = new();
        Assert.Equal("HOME", provider.GetHomeText());
    }

    // ── OftPeerCertificateName ───────────────────────────────────────────────

    /// <summary>Null config → auto name "USER-{userName}".</summary>
    [Fact]
    public void OftPeerCertificateName_NullConfig_ReturnsAutoName()
    {
        OftPeerCertificateName provider = new(new EngineConfig());
        Assert.Equal("USER-ALPHA", provider.GetCertificateName("ALPHA"));
    }

    /// <summary>"disable" config → null (no authentication).</summary>
    [Fact]
    public void OftPeerCertificateName_DisableConfig_ReturnsNull()
    {
        OftPeerCertificateName provider = new(new EngineConfig { PeerCertificateName = "disable" });
        Assert.Null(provider.GetCertificateName("ALPHA"));
    }

    /// <summary>Explicit name → that name regardless of user.</summary>
    [Fact]
    public void OftPeerCertificateName_ExplicitConfig_ReturnsExactName()
    {
        OftPeerCertificateName provider = new(new EngineConfig { PeerCertificateName = "MY-CERT" });
        Assert.Equal("MY-CERT", provider.GetCertificateName("ALPHA"));
        Assert.Equal("MY-CERT", provider.GetCertificateName("BETA"));
    }

    // ── PortConfiguration ─────────────────────────────────────────────────────

    /// <summary>Null ports fall back to defaults (50021 peer, 50020 interface).</summary>
    [Fact]
    public void PortConfiguration_NullPorts_UsesDefaults()
    {
        PortConfiguration ports = new(new EngineConfig());
        Assert.Equal(50021, ports.PeerPort);
        Assert.Equal(50020, ports.InterfacePort);
    }

    /// <summary>Configured ports override the defaults.</summary>
    [Fact]
    public void PortConfiguration_ConfiguredPorts_OverrideDefaults()
    {
        PortConfiguration ports = new(new EngineConfig { PeerPort = 9001, InterfacePort = 9002 });
        Assert.Equal(9001, ports.PeerPort);
        Assert.Equal(9002, ports.InterfacePort);
    }

    // ── UserCodeResolver ──────────────────────────────────────────────────────

    /// <summary>Resolves the hard-coded "CODE" code to a UserInfo.</summary>
    [Fact]
    public async Task UserCodeResolver_KnownCode_ReturnsUserInfo()
    {
        UserCodeResolver resolver = new();
        UserInfo? result = await resolver.Resolve("CODE");
        Assert.NotNull(result);
        Assert.Equal("TEST", result.Name);
        Assert.Equal("CODE", result.Code);
    }

    /// <summary>Case-insensitive: "code" resolves the same as "CODE".</summary>
    [Fact]
    public async Task UserCodeResolver_CaseInsensitive_Resolves()
    {
        UserCodeResolver resolver = new();
        UserInfo? result = await resolver.Resolve("code");
        Assert.NotNull(result);
    }

    /// <summary>Unknown code returns null.</summary>
    [Fact]
    public async Task UserCodeResolver_UnknownCode_ReturnsNull()
    {
        UserCodeResolver resolver = new();
        UserInfo? result = await resolver.Resolve("UNKNOWN");
        Assert.Null(result);
    }

    // ── UserLocator ───────────────────────────────────────────────────────────

    /// <summary>Returns endpoint for a configured user.</summary>
    [Fact]
    public async Task UserLocator_KnownUser_ReturnsEndpoint()
    {
        EngineConfig config = new()
        {
            Users = new Dictionary<string, UserEndpointConfig>
            {
                ["ALPHA"] = new UserEndpointConfig { IpAddress = "192.168.1.10", Port = 7890 }
            }
        };
        UserLocator locator = new(config);

        UserEndpoint? result = await locator.GetEndpoint("ALPHA");

        Assert.NotNull(result);
        Assert.Equal("192.168.1.10", result.IpAddress);
        Assert.Equal(7890, result.Port);
    }

    /// <summary>Returns null for an unknown user.</summary>
    [Fact]
    public async Task UserLocator_UnknownUser_ReturnsNull()
    {
        UserLocator locator = new(new EngineConfig());
        UserEndpoint? result = await locator.GetEndpoint("UNKNOWN");
        Assert.Null(result);
    }

    // ── UserNameDirectory ─────────────────────────────────────────────────────

    /// <summary>Combines user and group names, deduplicates, and sorts alphabetically.</summary>
    [Fact]
    public async Task UserNameDirectory_CombinesUsersAndGroups()
    {
        EngineConfig config = new()
        {
            Users = new Dictionary<string, UserEndpointConfig>
            {
                ["CHARLIE"] = new UserEndpointConfig { IpAddress = "1.2.3.4", Port = 1 },
                ["ALPHA"] = new UserEndpointConfig { IpAddress = "1.2.3.5", Port = 2 }
            },
            UserGroups = new Dictionary<string, List<string>>
            {
                ["BRAVO"] = ["ALPHA"]
            }
        };
        UserNameDirectory dir = new(config);

        IReadOnlyList<string> names = await dir.GetAllUserNames();

        Assert.Equal(["ALPHA", "BRAVO", "CHARLIE"], names);
    }

    /// <summary>Empty config produces an empty list.</summary>
    [Fact]
    public async Task UserNameDirectory_EmptyConfig_ReturnsEmptyList()
    {
        UserNameDirectory dir = new(new EngineConfig());
        IReadOnlyList<string> names = await dir.GetAllUserNames();
        Assert.Empty(names);
    }
}
