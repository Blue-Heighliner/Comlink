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

    // ── DebugSiteOverride ─────────────────────────────────────────────────────

    /// <summary>SiteName returns the value from config.</summary>
    [Fact]
    public void DebugSiteOverride_ReturnsSiteNameFromConfig()
    {
        DebugSiteOverride provider = new(new EngineConfig { SiteName = "ALPHA" });
        Assert.Equal("ALPHA", provider.SiteName);
    }

    /// <summary>SiteName is null when config has no override.</summary>
    [Fact]
    public void DebugSiteOverride_NullWhenNotConfigured()
    {
        DebugSiteOverride provider = new(new EngineConfig());
        Assert.Null(provider.SiteName);
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

    /// <summary>Null config → auto name "SITE-{siteName}".</summary>
    [Fact]
    public void OftPeerCertificateName_NullConfig_ReturnsAutoName()
    {
        OftPeerCertificateName provider = new(new EngineConfig());
        Assert.Equal("SITE-ALPHA", provider.GetCertificateName("ALPHA"));
    }

    /// <summary>"disable" config → null (no authentication).</summary>
    [Fact]
    public void OftPeerCertificateName_DisableConfig_ReturnsNull()
    {
        OftPeerCertificateName provider = new(new EngineConfig { PeerCertificateName = "disable" });
        Assert.Null(provider.GetCertificateName("ALPHA"));
    }

    /// <summary>Explicit name → that name regardless of site.</summary>
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

    // ── SiteCodeResolver ──────────────────────────────────────────────────────

    /// <summary>Resolves the hard-coded "CODE" code to a SiteInfo.</summary>
    [Fact]
    public async Task SiteCodeResolver_KnownCode_ReturnsSiteInfo()
    {
        SiteCodeResolver resolver = new();
        SiteInfo? result = await resolver.Resolve("CODE");
        Assert.NotNull(result);
        Assert.Equal("TEST", result.Name);
        Assert.Equal("CODE", result.Code);
    }

    /// <summary>Case-insensitive: "code" resolves the same as "CODE".</summary>
    [Fact]
    public async Task SiteCodeResolver_CaseInsensitive_Resolves()
    {
        SiteCodeResolver resolver = new();
        SiteInfo? result = await resolver.Resolve("code");
        Assert.NotNull(result);
    }

    /// <summary>Unknown code returns null.</summary>
    [Fact]
    public async Task SiteCodeResolver_UnknownCode_ReturnsNull()
    {
        SiteCodeResolver resolver = new();
        SiteInfo? result = await resolver.Resolve("UNKNOWN");
        Assert.Null(result);
    }

    // ── SiteLocator ───────────────────────────────────────────────────────────

    /// <summary>Returns endpoint for a configured site.</summary>
    [Fact]
    public async Task SiteLocator_KnownSite_ReturnsEndpoint()
    {
        EngineConfig config = new()
        {
            Sites = new Dictionary<string, SiteEndpointConfig>
            {
                ["ALPHA"] = new SiteEndpointConfig { IpAddress = "192.168.1.10", Port = 7890 }
            }
        };
        SiteLocator locator = new(config);

        SiteEndpoint? result = await locator.GetEndpoint("ALPHA");

        Assert.NotNull(result);
        Assert.Equal("192.168.1.10", result.IpAddress);
        Assert.Equal(7890, result.Port);
    }

    /// <summary>Returns null for an unknown site.</summary>
    [Fact]
    public async Task SiteLocator_UnknownSite_ReturnsNull()
    {
        SiteLocator locator = new(new EngineConfig());
        SiteEndpoint? result = await locator.GetEndpoint("UNKNOWN");
        Assert.Null(result);
    }

    // ── SiteNameDirectory ─────────────────────────────────────────────────────

    /// <summary>Combines site and group names, deduplicates, and sorts alphabetically.</summary>
    [Fact]
    public async Task SiteNameDirectory_CombinesSitesAndGroups()
    {
        EngineConfig config = new()
        {
            Sites = new Dictionary<string, SiteEndpointConfig>
            {
                ["CHARLIE"] = new SiteEndpointConfig { IpAddress = "1.2.3.4", Port = 1 },
                ["ALPHA"] = new SiteEndpointConfig { IpAddress = "1.2.3.5", Port = 2 }
            },
            SiteGroups = new Dictionary<string, List<string>>
            {
                ["BRAVO"] = ["ALPHA"]
            }
        };
        SiteNameDirectory dir = new(config);

        IReadOnlyList<string> names = await dir.GetAllSiteNames();

        Assert.Equal(["ALPHA", "BRAVO", "CHARLIE"], names);
    }

    /// <summary>Empty config produces an empty list.</summary>
    [Fact]
    public async Task SiteNameDirectory_EmptyConfig_ReturnsEmptyList()
    {
        SiteNameDirectory dir = new(new EngineConfig());
        IReadOnlyList<string> names = await dir.GetAllSiteNames();
        Assert.Empty(names);
    }
}
