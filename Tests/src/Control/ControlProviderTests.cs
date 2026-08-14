namespace BlueHeighliner.Comlink.Tests.Control;

/// <summary>
/// Unit tests for the default control-interface implementations in <see cref="Engine.Control"/> and their
/// corresponding <c>Configured*</c> engine-level <see cref="EngineConfig"/> decorators.
/// </summary>
public sealed class ControlProviderTests
{
    // ── AppSettings ───────────────────────────────────────────────────────────

    private static string SystemAppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    /// <summary>The default implementation derives AppDataPath from AppName via virtual dispatch, and returns hardcoded defaults for everything else.</summary>
    [Fact]
    public void DefaultAppSettings_UsesAppNameAndHardcodedDefaults()
    {
        DefaultAppSettings settings = new();
        Assert.Equal(Path.Combine(SystemAppData, settings.AppName), settings.AppDataPath);
        Assert.False(settings.IsKioskMode);
        Assert.Equal("HOME", settings.GetHomeText());
    }

    /// <summary>A subclass overriding only AppName automatically gets a matching AppDataPath, since the base computes it via virtual dispatch.</summary>
    [Fact]
    public void DefaultAppSettings_OverridingAppNameOnly_AppDataPathFollows()
    {
        TestAppNameOverride settings = new();
        Assert.Equal(Path.Combine(SystemAppData, "CustomApp"), settings.AppDataPath);
    }

    private sealed class TestAppNameOverride : DefaultAppSettings
    {
        public override string AppName => "CustomApp";
    }

    /// <summary>Null DataFolder falls back to the wrapped provider's own path.</summary>
    [Fact]
    public void ConfiguredAppSettings_NullDataFolder_FallsBack()
    {
        Mock<IAppSettings> fallback = new();
        fallback.Setup(f => f.AppDataPath).Returns("/base/path");
        ConfiguredAppSettings settings = new(fallback.Object, new EngineConfig());

        Assert.Equal("/base/path", settings.AppDataPath);
    }

    /// <summary>DataFolder starting with '@' is treated as relative to the fallback's own AppDataPath.</summary>
    [Fact]
    public void ConfiguredAppSettings_AtPrefix_IsRelativeToFallback()
    {
        Mock<IAppSettings> fallback = new();
        fallback.Setup(f => f.AppDataPath).Returns("/base/path");
        ConfiguredAppSettings settings = new(fallback.Object, new EngineConfig { DataFolder = "@test/sub" });

        Assert.Equal(Path.Combine("/base/path", "test", "sub"), settings.AppDataPath);
    }

    /// <summary>An absolute DataFolder path is used verbatim.</summary>
    [Fact]
    public void ConfiguredAppSettings_AbsolutePath_UsedDirectly()
    {
        Mock<IAppSettings> fallback = new();
        string absolute = "/tmp/custom-data";
        ConfiguredAppSettings settings = new(fallback.Object, new EngineConfig { DataFolder = absolute });

        Assert.Equal(absolute, settings.AppDataPath);
    }

    /// <summary>AppName, IsKioskMode, and GetHomeText are left entirely to the fallback, since none has a config.json field.</summary>
    [Fact]
    public void ConfiguredAppSettings_NonConfigMembers_AlwaysDelegateToFallback()
    {
        Mock<IAppSettings> fallback = new();
        fallback.Setup(f => f.AppName).Returns("FallbackApp");
        fallback.Setup(f => f.IsKioskMode).Returns(true);
        fallback.Setup(f => f.GetHomeText()).Returns("FALLBACK-HOME");
        ConfiguredAppSettings settings = new(fallback.Object, new EngineConfig());

        Assert.Equal("FallbackApp", settings.AppName);
        Assert.True(settings.IsKioskMode);
        Assert.Equal("FALLBACK-HOME", settings.GetHomeText());
    }

    // ── UserIdentity ──────────────────────────────────────────────────────────

    /// <summary>The default implementation has no debug override and only resolves the hard-coded "CODE" code.</summary>
    [Fact]
    public async Task DefaultUserIdentity_NoDebugOverride_ResolvesOnlyCode()
    {
        DefaultUserIdentity identity = new();
        Assert.Null(identity.DebugUserName);

        UserInfo? result = await identity.ResolveCode("CODE");
        Assert.NotNull(result);
        Assert.Equal("TEST", result.Name);

        Assert.Null(await identity.ResolveCode("UNKNOWN"));
    }

    /// <summary>DebugUserName returns the value from config.</summary>
    [Fact]
    public void ConfiguredUserIdentity_ReturnsDebugUserNameFromConfig()
    {
        ConfiguredUserIdentity identity = new(new DefaultUserIdentity(), new EngineConfig { UserName = "ALPHA" });
        Assert.Equal("ALPHA", identity.DebugUserName);
    }

    /// <summary>DebugUserName falls back to the wrapped provider when config has no override.</summary>
    [Fact]
    public void ConfiguredUserIdentity_FallsBackWhenNotConfigured()
    {
        Mock<IUserIdentity> fallback = new();
        fallback.Setup(f => f.DebugUserName).Returns("FALLBACK");
        ConfiguredUserIdentity identity = new(fallback.Object, new EngineConfig());

        Assert.Equal("FALLBACK", identity.DebugUserName);
    }

    /// <summary>ResolveCode is left entirely to the wrapped provider, since there is no corresponding config.json field.</summary>
    [Fact]
    public async Task ConfiguredUserIdentity_ResolveCode_AlwaysDelegatesToFallback()
    {
        UserInfo fallbackInfo = new() { Name = "X", Code = "Y", EnvironmentTitle = "Z", EnvironmentColor = "#000" };
        Mock<IUserIdentity> fallback = new();
        fallback.Setup(f => f.ResolveCode("ANY", default)).ReturnsAsync(fallbackInfo);
        ConfiguredUserIdentity identity = new(fallback.Object, new EngineConfig());

        Assert.Same(fallbackInfo, await identity.ResolveCode("ANY"));
    }

    // ── KioskModeProvider (via AppSettings now; retained standalone default tests above) ──

    // ── HomeContentProvider (via AppSettings now; see DefaultAppSettings tests above) ──

    // ── MessageComposition ────────────────────────────────────────────────────

    /// <summary>The default implementation offers a single "Normal" priority level, tags enabled with label "Tag", and no blocked combinations.</summary>
    [Fact]
    public void DefaultMessageComposition_ReturnsHardcodedDefaults()
    {
        DefaultMessageComposition composition = new();

        IReadOnlyList<MessagePriorityOption> priorities = composition.GetPriorities();
        Assert.Equal(["Normal"], priorities.Select(p => p.Name).ToList());
        Assert.Equal([0], priorities.Select(p => p.Value).ToList());
        Assert.True(composition.TagsEnabled);
        Assert.Equal("Tag", composition.TagLabel);
        Assert.Empty(composition.GetBlockedCombinations());
    }

    /// <summary>GetPriorities returns the same list instance/values on every call.</summary>
    [Fact]
    public void DefaultMessageComposition_GetPriorities_IsStableAcrossCalls()
    {
        DefaultMessageComposition composition = new();
        Assert.Equal(composition.GetPriorities(), composition.GetPriorities());
    }

    /// <summary>Falls back to the wrapped provider for TagsEnabled/TagLabel when not configured; GetPriorities/GetBlockedCombinations always delegate.</summary>
    [Fact]
    public void ConfiguredMessageComposition_FallsBackWhenNotConfigured()
    {
        Mock<IMessageComposition> fallback = new();
        fallback.Setup(f => f.TagsEnabled).Returns(false);
        fallback.Setup(f => f.TagLabel).Returns("Category");
        IReadOnlyList<MessagePriorityOption> priorities = [new MessagePriorityOption { Name = "Low", Value = 0 }];
        fallback.Setup(f => f.GetPriorities()).Returns(priorities);
        IReadOnlyList<TagPriorityBlock> blocks = [new TagPriorityBlock { Tag = "SPAM" }];
        fallback.Setup(f => f.GetBlockedCombinations()).Returns(blocks);
        ConfiguredMessageComposition composition = new(fallback.Object, new EngineConfig());

        Assert.False(composition.TagsEnabled);
        Assert.Equal("Category", composition.TagLabel);
        Assert.Same(priorities, composition.GetPriorities());
        Assert.Same(blocks, composition.GetBlockedCombinations());
    }

    /// <summary>TagsEnabled reflects an explicit false override from config.</summary>
    [Fact]
    public void ConfiguredMessageComposition_ReturnsFalseWhenDisabledInConfig()
    {
        ConfiguredMessageComposition composition = new(new DefaultMessageComposition(), new EngineConfig { MessageTagsEnabled = false });
        Assert.False(composition.TagsEnabled);
    }

    /// <summary>TagLabel reflects an explicit override from config.</summary>
    [Fact]
    public void ConfiguredMessageComposition_TagLabel_ReturnsConfiguredValue()
    {
        ConfiguredMessageComposition composition = new(new DefaultMessageComposition(), new EngineConfig { MessageTagLabel = "Category" });
        Assert.Equal("Category", composition.TagLabel);
    }

    // ── TagPriorityBlockExtensions ────────────────────────────────────────────

    /// <summary>A rule with only Tag set blocks that tag regardless of priority.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(99)]
    public void TagPriorityBlockExtensions_IsBlocked_TagWithNullPriority_BlocksAnyPriority(int priority)
    {
        IReadOnlyList<TagPriorityBlock> blocks = [new TagPriorityBlock { Tag = "SPAM", Priority = null }];
        Assert.True(blocks.IsBlocked("SPAM", priority));
    }

    /// <summary>A rule with only Priority set blocks that priority regardless of tag.</summary>
    [Theory]
    [InlineData("URGENT")]
    [InlineData("")]
    [InlineData(null)]
    public void TagPriorityBlockExtensions_IsBlocked_PriorityWithNullTag_BlocksAnyTag(string? tag)
    {
        IReadOnlyList<TagPriorityBlock> blocks = [new TagPriorityBlock { Tag = null, Priority = 2 }];
        Assert.True(blocks.IsBlocked(tag, 2));
    }

    /// <summary>A rule with both fields set only blocks that exact tag/priority pair.</summary>
    [Fact]
    public void TagPriorityBlockExtensions_IsBlocked_SpecificPair_OnlyBlocksExactMatch()
    {
        IReadOnlyList<TagPriorityBlock> blocks = [new TagPriorityBlock { Tag = "URGENT", Priority = 2 }];

        Assert.True(blocks.IsBlocked("URGENT", 2));
        Assert.False(blocks.IsBlocked("URGENT", 1));
        Assert.False(blocks.IsBlocked("OTHER", 2));
    }

    /// <summary>Tag matching is case-insensitive.</summary>
    [Fact]
    public void TagPriorityBlockExtensions_IsBlocked_TagMatchIsCaseInsensitive()
    {
        IReadOnlyList<TagPriorityBlock> blocks = [new TagPriorityBlock { Tag = "SPAM", Priority = null }];
        Assert.True(blocks.IsBlocked("spam", 0));
    }

    /// <summary>No rule matches → not blocked.</summary>
    [Fact]
    public void TagPriorityBlockExtensions_IsBlocked_NoMatchingRule_ReturnsFalse()
    {
        IReadOnlyList<TagPriorityBlock> blocks = [new TagPriorityBlock { Tag = "SPAM", Priority = null }];
        Assert.False(blocks.IsBlocked("OK", 0));
    }

    /// <summary>GetLabel returns the matching option's Name.</summary>
    [Fact]
    public void MessagePriorityOptionExtensions_GetLabel_ReturnsMatchingName()
    {
        IReadOnlyList<MessagePriorityOption> priorities =
        [
            new MessagePriorityOption { Name = "Low", Value = 0 },
            new MessagePriorityOption { Name = "High", Value = 2 }
        ];

        Assert.Equal("High", priorities.GetLabel(2));
    }

    /// <summary>GetLabel falls back to the plain numeric value when no option matches.</summary>
    [Fact]
    public void MessagePriorityOptionExtensions_GetLabel_NoMatch_FallsBackToNumber()
    {
        IReadOnlyList<MessagePriorityOption> priorities = [new MessagePriorityOption { Name = "Normal", Value = 0 }];

        Assert.Equal("99", priorities.GetLabel(99));
    }

    // ── AlertSettings ─────────────────────────────────────────────────────────

    /// <summary>The default implementation returns hardcoded settings.</summary>
    [Fact]
    public void DefaultAlertSettings_ReturnsHardcodedDefaults()
    {
        DefaultAlertSettings settings = new();
        Assert.Equal("ALERT", settings.AlertText);
        Assert.Equal(TimeSpan.FromSeconds(30), settings.AlarmSoundDuration);
        Assert.True(settings.QuickConfirmationEnabled);
        Assert.True(settings.ComposeAlertsEnabled);
    }

    /// <summary>Falls back to the wrapped provider for every field when not configured.</summary>
    [Fact]
    public void ConfiguredAlertSettings_FallsBackWhenNotConfigured()
    {
        Mock<IAlertSettings> fallback = new();
        fallback.Setup(f => f.AlertText).Returns("FALLBACK");
        fallback.Setup(f => f.AlarmSoundDuration).Returns(TimeSpan.FromSeconds(12));
        fallback.Setup(f => f.QuickConfirmationEnabled).Returns(false);
        fallback.Setup(f => f.ComposeAlertsEnabled).Returns(false);
        ConfiguredAlertSettings settings = new(fallback.Object, new EngineConfig());

        Assert.Equal("FALLBACK", settings.AlertText);
        Assert.Equal(TimeSpan.FromSeconds(12), settings.AlarmSoundDuration);
        Assert.False(settings.QuickConfirmationEnabled);
        Assert.False(settings.ComposeAlertsEnabled);
    }

    /// <summary>Every settable field reflects an explicit override from config.</summary>
    [Fact]
    public void ConfiguredAlertSettings_OverridesFromConfig()
    {
        ConfiguredAlertSettings settings = new(new DefaultAlertSettings(), new EngineConfig
        {
            AlertText = "URGENT",
            AlarmSoundSeconds = 5,
            QuickConfirmationEnabled = false,
            ComposeAlertsEnabled = false
        });

        Assert.Equal("URGENT", settings.AlertText);
        Assert.Equal(TimeSpan.FromSeconds(5), settings.AlarmSoundDuration);
        Assert.False(settings.QuickConfirmationEnabled);
        Assert.False(settings.ComposeAlertsEnabled);
    }

    // PrinterProvider (also the ILinePrinter implementation — see Docs/Control.md) is not unit tested
    // directly: it shells out to the operating system's own printing facilities (WinSpool via P/Invoke on
    // Windows, lp/lpstat/CUPS on Linux), so its behavior is inherently environment- and OS-dependent — the
    // same reasoning that leaves OftCertificateProvider untested here.

    // ── PrintPolicy ───────────────────────────────────────────────────────────

    /// <summary>The default implementation is disabled by default and prints every message exactly once.</summary>
    [Fact]
    public void DefaultPrintPolicy_ReturnsHardcodedDefaults()
    {
        DefaultPrintPolicy policy = new();
        Assert.False(policy.PrintReceivedDefaultEnabled);
        Assert.Equal(1, policy.GetPrintCount(new object()));
    }

    /// <summary>Falls back to the wrapped provider when not configured.</summary>
    [Fact]
    public void ConfiguredPrintPolicy_FallsBackWhenNotConfigured()
    {
        Mock<IPrintPolicy> fallback = new();
        fallback.Setup(f => f.PrintReceivedDefaultEnabled).Returns(true);
        ConfiguredPrintPolicy policy = new(fallback.Object, new EngineConfig());

        Assert.True(policy.PrintReceivedDefaultEnabled);
    }

    /// <summary>PrintReceivedDefaultEnabled reflects an explicit true override from config.</summary>
    [Fact]
    public void ConfiguredPrintPolicy_ReturnsTrueWhenEnabledInConfig()
    {
        ConfiguredPrintPolicy policy = new(new DefaultPrintPolicy(), new EngineConfig { PrintReceivedEnabled = true });
        Assert.True(policy.PrintReceivedDefaultEnabled);
    }

    /// <summary>GetPrintCount always delegates to the wrapped provider, since there is no corresponding config.json field.</summary>
    [Fact]
    public void ConfiguredPrintPolicy_GetPrintCount_AlwaysDelegatesToFallback()
    {
        Mock<IPrintPolicy> fallback = new();
        fallback.Setup(f => f.GetPrintCount(It.IsAny<object>())).Returns(5);
        ConfiguredPrintPolicy policy = new(fallback.Object, new EngineConfig());

        Assert.Equal(5, policy.GetPrintCount(new object()));
    }

    // ── OftPeerCertificateName / ConfiguredOftPeerCertificateName ───────────────

    /// <summary>The default implementation always returns the auto-detected name.</summary>
    [Fact]
    public void OftPeerCertificateName_AlwaysReturnsAutoName()
    {
        DefaultOftPeerCertificateName provider = new();
        Assert.Equal("USER-ALPHA", provider.GetCertificateName("ALPHA"));
    }

    /// <summary>Null config falls back to the wrapped provider.</summary>
    [Fact]
    public void ConfiguredOftPeerCertificateName_NullConfig_FallsBack()
    {
        Mock<IOftPeerCertificateName> fallback = new();
        fallback.Setup(f => f.GetCertificateName("ALPHA")).Returns("FALLBACK-NAME");
        ConfiguredOftPeerCertificateName provider = new(fallback.Object, new EngineConfig());

        Assert.Equal("FALLBACK-NAME", provider.GetCertificateName("ALPHA"));
    }

    /// <summary>"disable" config → null (no authentication), regardless of the wrapped provider.</summary>
    [Fact]
    public void ConfiguredOftPeerCertificateName_DisableConfig_ReturnsNull()
    {
        ConfiguredOftPeerCertificateName provider = new(new DefaultOftPeerCertificateName(), new EngineConfig { PeerCertificateName = "disable" });
        Assert.Null(provider.GetCertificateName("ALPHA"));
    }

    /// <summary>Explicit name → that name regardless of user.</summary>
    [Fact]
    public void ConfiguredOftPeerCertificateName_ExplicitConfig_ReturnsExactName()
    {
        ConfiguredOftPeerCertificateName provider = new(new DefaultOftPeerCertificateName(), new EngineConfig { PeerCertificateName = "MY-CERT" });
        Assert.Equal("MY-CERT", provider.GetCertificateName("ALPHA"));
        Assert.Equal("MY-CERT", provider.GetCertificateName("BETA"));
    }

    // ── PortConfiguration / ConfiguredPortConfiguration ──────────────────────────

    /// <summary>The default implementation always returns the well-known default ports.</summary>
    [Fact]
    public void PortConfiguration_UsesDefaults()
    {
        DefaultPortConfiguration ports = new();
        Assert.Equal(50021, ports.PeerPort);
        Assert.Equal(50020, ports.InterfacePort);
    }

    /// <summary>Null ports fall back to the wrapped provider.</summary>
    [Fact]
    public void ConfiguredPortConfiguration_NullPorts_FallsBack()
    {
        Mock<IPortConfiguration> fallback = new();
        fallback.Setup(f => f.PeerPort).Returns(11111);
        fallback.Setup(f => f.InterfacePort).Returns(22222);
        ConfiguredPortConfiguration ports = new(fallback.Object, new EngineConfig());

        Assert.Equal(11111, ports.PeerPort);
        Assert.Equal(22222, ports.InterfacePort);
    }

    /// <summary>Configured ports override the fallback.</summary>
    [Fact]
    public void ConfiguredPortConfiguration_ConfiguredPorts_OverrideFallback()
    {
        ConfiguredPortConfiguration ports = new(new DefaultPortConfiguration(), new EngineConfig { PeerPort = 9001, InterfacePort = 9002 });
        Assert.Equal(9001, ports.PeerPort);
        Assert.Equal(9002, ports.InterfacePort);
    }

    // ── UserDirectory ─────────────────────────────────────────────────────────

    /// <summary>The default implementation never knows about any user, group, or name.</summary>
    [Fact]
    public async Task DefaultUserDirectory_AlwaysEmpty()
    {
        DefaultUserDirectory directory = new();
        Assert.Null(await directory.GetEndpoint("ANY"));
        Assert.Empty(await directory.GetGroups());
        Assert.Empty(await directory.GetAllUserNames());
    }

    /// <summary>Returns endpoint for a configured user.</summary>
    [Fact]
    public async Task ConfiguredUserDirectory_KnownUser_ReturnsEndpoint()
    {
        EngineConfig config = new()
        {
            Users = new Dictionary<string, UserEndpointConfig>
            {
                ["ALPHA"] = new UserEndpointConfig { IpAddress = "192.168.1.10", Port = 7890 }
            }
        };
        ConfiguredUserDirectory directory = new(new DefaultUserDirectory(), config);

        UserEndpoint? result = await directory.GetEndpoint("ALPHA");

        Assert.NotNull(result);
        Assert.Equal("192.168.1.10", result.IpAddress);
        Assert.Equal(7890, result.Port);
    }

    /// <summary>Falls back to the wrapped provider for an unknown user.</summary>
    [Fact]
    public async Task ConfiguredUserDirectory_UnknownUser_FallsBack()
    {
        UserEndpoint fallbackEndpoint = new() { IpAddress = "10.0.0.1", Port = 1 };
        Mock<IUserDirectory> fallback = new();
        fallback.Setup(f => f.GetEndpoint("UNKNOWN", default)).ReturnsAsync(fallbackEndpoint);
        ConfiguredUserDirectory directory = new(fallback.Object, new EngineConfig());

        Assert.Same(fallbackEndpoint, await directory.GetEndpoint("UNKNOWN"));
    }

    /// <summary>Config groups are merged over the fallback's own groups, config winning on key conflicts.</summary>
    [Fact]
    public async Task ConfiguredUserDirectory_MergesGroupsConfigOverFallback()
    {
        Mock<IUserDirectory> fallback = new();
        fallback.Setup(f => f.GetGroups(default)).ReturnsAsync((IReadOnlyDictionary<string, IReadOnlyList<string>>)
            new Dictionary<string, IReadOnlyList<string>> { ["OPS"] = ["FALLBACK-USER"], ["ONLY-FALLBACK"] = ["X"] });

        EngineConfig config = new()
        {
            UserGroups = new Dictionary<string, List<string>> { ["OPS"] = ["ALPHA", "BETA"] }
        };
        ConfiguredUserDirectory directory = new(fallback.Object, config);

        IReadOnlyDictionary<string, IReadOnlyList<string>> groups = await directory.GetGroups();

        Assert.Equal(["ALPHA", "BETA"], groups["OPS"]);
        Assert.Equal(["X"], groups["ONLY-FALLBACK"]);
    }

    /// <summary>Combines the fallback's names with configured user and group names, deduplicates, and sorts alphabetically.</summary>
    [Fact]
    public async Task ConfiguredUserDirectory_CombinesFallbackUsersAndGroupNames()
    {
        Mock<IUserDirectory> fallback = new();
        fallback.Setup(f => f.GetAllUserNames(default)).ReturnsAsync((IReadOnlyList<string>)["DELTA"]);

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
        ConfiguredUserDirectory directory = new(fallback.Object, config);

        IReadOnlyList<string> names = await directory.GetAllUserNames();

        Assert.Equal(["ALPHA", "BRAVO", "CHARLIE", "DELTA"], names);
    }

    /// <summary>Empty config produces just the fallback's names.</summary>
    [Fact]
    public async Task ConfiguredUserDirectory_EmptyConfig_ReturnsFallbackOnly()
    {
        ConfiguredUserDirectory directory = new(new DefaultUserDirectory(), new EngineConfig());
        Assert.Empty(await directory.GetAllUserNames());
    }

    // ── NetworkTopology ───────────────────────────────────────────────────────

    /// <summary>The default implementation is always Peer with no server endpoint or server users configured.</summary>
    [Fact]
    public async Task DefaultNetworkTopology_PeerWithNothingConfigured()
    {
        DefaultNetworkTopology topology = new();
        Assert.Equal(NodeRole.Peer, topology.Role);
        Assert.Null(topology.GetServerEndpoint());
        Assert.Empty(await topology.GetServerUsers());
    }

    /// <summary>Falls back to the wrapped provider when config does not set a role.</summary>
    [Fact]
    public void ConfiguredNetworkTopology_FallsBackWhenRoleNotConfigured()
    {
        Mock<INetworkTopology> fallback = new();
        fallback.Setup(f => f.Role).Returns(NodeRole.Server);
        ConfiguredNetworkTopology topology = new(fallback.Object, new EngineConfig());

        Assert.Equal(NodeRole.Server, topology.Role);
    }

    /// <summary>An unrecognized config role string falls back to the wrapped provider.</summary>
    [Fact]
    public void ConfiguredNetworkTopology_UnrecognizedRole_FallsBack()
    {
        Mock<INetworkTopology> fallback = new();
        fallback.Setup(f => f.Role).Returns(NodeRole.Client);
        ConfiguredNetworkTopology topology = new(fallback.Object, new EngineConfig { NodeRole = "Bogus" });

        Assert.Equal(NodeRole.Client, topology.Role);
    }

    /// <summary>A recognized config role overrides the fallback.</summary>
    [Fact]
    public void ConfiguredNetworkTopology_RecognizedRole_Overrides()
    {
        ConfiguredNetworkTopology topology = new(new DefaultNetworkTopology(), new EngineConfig { NodeRole = "Server" });
        Assert.Equal(NodeRole.Server, topology.Role);
    }

    /// <summary>Falls back to the wrapped provider when config does not set an endpoint.</summary>
    [Fact]
    public void ConfiguredNetworkTopology_FallsBackWhenEndpointNotConfigured()
    {
        UserEndpoint fallbackEndpoint = new() { IpAddress = "10.0.0.1", Port = 1 };
        Mock<INetworkTopology> fallback = new();
        fallback.Setup(f => f.GetServerEndpoint()).Returns(fallbackEndpoint);
        ConfiguredNetworkTopology topology = new(fallback.Object, new EngineConfig());

        Assert.Same(fallbackEndpoint, topology.GetServerEndpoint());
    }

    /// <summary>A configured endpoint overrides the fallback.</summary>
    [Fact]
    public void ConfiguredNetworkTopology_EndpointOverridesFromConfig()
    {
        EngineConfig config = new() { ServerEndpoint = new UserEndpointConfig { IpAddress = "10.0.0.5", Port = 9000 } };
        ConfiguredNetworkTopology topology = new(new DefaultNetworkTopology(), config);

        UserEndpoint? result = topology.GetServerEndpoint();

        Assert.NotNull(result);
        Assert.Equal("10.0.0.5", result.IpAddress);
        Assert.Equal(9000, result.Port);
    }

    /// <summary>Config server users are merged over the fallback's own, config winning on key conflicts.</summary>
    [Fact]
    public async Task ConfiguredNetworkTopology_MergesServerUsersConfigOverFallback()
    {
        Mock<INetworkTopology> fallback = new();
        fallback.Setup(f => f.GetServerUsers(default)).ReturnsAsync((IReadOnlyDictionary<string, ServerUserConfig>)
            new Dictionary<string, ServerUserConfig>
            {
                ["SERVER-A"] = new ServerUserConfig { Endpoint = new UserEndpoint { IpAddress = "1.1.1.1", Port = 1 }, ChildClients = ["FALLBACK-CHILD"] },
                ["ONLY-FALLBACK"] = new ServerUserConfig { Endpoint = new UserEndpoint { IpAddress = "2.2.2.2", Port = 2 }, ChildClients = [] }
            });

        EngineConfig config = new()
        {
            ServerUsers = new Dictionary<string, ServerUserConfigEntry>
            {
                ["SERVER-A"] = new ServerUserConfigEntry { IpAddress = "9.9.9.9", Port = 9, ChildClients = ["CONFIG-CHILD"] }
            }
        };
        ConfiguredNetworkTopology topology = new(fallback.Object, config);

        IReadOnlyDictionary<string, ServerUserConfig> servers = await topology.GetServerUsers();

        Assert.Equal("9.9.9.9", servers["SERVER-A"].Endpoint.IpAddress);
        Assert.Equal(["CONFIG-CHILD"], servers["SERVER-A"].ChildClients);
        Assert.Equal("2.2.2.2", servers["ONLY-FALLBACK"].Endpoint.IpAddress);
    }

    // ── ConfigFileProvider ────────────────────────────────────────────────────

    /// <summary>The default implementation always enables config file reading.</summary>
    [Fact]
    public void ConfigFileProvider_AlwaysEnabled()
    {
        DefaultConfigFileProvider provider = new();
        Assert.True(provider.Enabled);
    }
}
