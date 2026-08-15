namespace BlueHeighliner.Comlink.Tests.Control;

/// <summary>
/// Unit tests for <see cref="DefaultEngineController{TMessage}"/> (via the <see cref="TestEngineController"/> test double) and its corresponding <see cref="ConfiguredEngineController"/>
/// engine-level <see cref="EngineConfig"/> decorator.
/// </summary>
public sealed class ControlProviderTests
{
    private static string SystemAppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static ICurrentUserProvider NoCurrentUser => new CurrentUserProvider();

    /// <summary>The default implementation derives AppDataPath from AppName via virtual dispatch, and returns hardcoded defaults for everything else.</summary>
    [Fact]
    public void DefaultEngineController_UsesAppNameAndHardcodedDefaults()
    {
        TestEngineController controller = new();
        Assert.Equal(Path.Combine(SystemAppData, controller.AppName), controller.AppDataPath);
        Assert.False(controller.IsKioskMode);
        Assert.Equal("HOME", controller.HomeText);
    }

    /// <summary>A subclass overriding only AppName automatically gets a matching AppDataPath, since the base computes it via virtual dispatch.</summary>
    [Fact]
    public void DefaultEngineController_OverridingAppNameOnly_AppDataPathFollows()
    {
        TestAppNameOverride controller = new();
        Assert.Equal(Path.Combine(SystemAppData, "CustomApp"), controller.AppDataPath);
    }

    private sealed class TestAppNameOverride : TestEngineController
    {
        public override string AppName => "CustomApp";
    }

    /// <summary>Null DataFolder falls back to the wrapped provider's own path.</summary>
    [Fact]
    public void ConfiguredEngineController_NullDataFolder_FallsBack()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.AppDataPath).Returns("/base/path");
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.Equal("/base/path", controller.AppDataPath);
    }

    /// <summary>DataFolder starting with '@' is treated as relative to the fallback's own AppDataPath.</summary>
    [Fact]
    public void ConfiguredEngineController_AtPrefix_IsRelativeToFallback()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.AppDataPath).Returns("/base/path");
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig { DataFolder = "@test/sub" }, NoCurrentUser);

        Assert.Equal(Path.Combine("/base/path", "test", "sub"), controller.AppDataPath);
    }

    /// <summary>An absolute DataFolder path is used verbatim.</summary>
    [Fact]
    public void ConfiguredEngineController_AbsolutePath_UsedDirectly()
    {
        Mock<IEngineController> fallback = new();
        string absolute = "/tmp/custom-data";
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig { DataFolder = absolute }, NoCurrentUser);

        Assert.Equal(absolute, controller.AppDataPath);
    }

    /// <summary>AppName, IsKioskMode, and HomeText are left entirely to the fallback, since none has a config.json field.</summary>
    [Fact]
    public void ConfiguredEngineController_NonConfigAppSettingsMembers_AlwaysDelegateToFallback()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.AppName).Returns("FallbackApp");
        fallback.Setup(f => f.IsKioskMode).Returns(true);
        fallback.Setup(f => f.HomeText).Returns("FALLBACK-HOME");
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.Equal("FallbackApp", controller.AppName);
        Assert.True(controller.IsKioskMode);
        Assert.Equal("FALLBACK-HOME", controller.HomeText);
    }

    /// <summary>The default implementation has no debug override and only resolves the hard-coded "CODE" code.</summary>
    [Fact]
    public void DefaultEngineController_NoDebugOverride_ResolvesOnlyCode()
    {
        TestEngineController controller = new();
        Assert.Null(controller.DebugUserName);

        UserInfo? result = controller.ResolveCode("CODE");
        Assert.NotNull(result);
        Assert.Equal("TEST", result.Name);

        Assert.Null(controller.ResolveCode("UNKNOWN"));
    }

    /// <summary>DebugUserName returns the value from config.</summary>
    [Fact]
    public void ConfiguredEngineController_ReturnsDebugUserNameFromConfig()
    {
        ConfiguredEngineController controller = new(new TestEngineController(), new EngineConfig { UserName = "ALPHA" }, NoCurrentUser);
        Assert.Equal("ALPHA", controller.DebugUserName);
    }

    /// <summary>DebugUserName falls back to the wrapped provider when config has no override.</summary>
    [Fact]
    public void ConfiguredEngineController_FallsBackWhenDebugUserNameNotConfigured()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.DebugUserName).Returns("FALLBACK");
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.Equal("FALLBACK", controller.DebugUserName);
    }

    /// <summary>ResolveCode is left entirely to the wrapped provider, since there is no corresponding config.json field.</summary>
    [Fact]
    public void ConfiguredEngineController_ResolveCode_AlwaysDelegatesToFallback()
    {
        UserInfo fallbackInfo = new() { Name = "X", Code = "Y", EnvironmentTitle = "Z", EnvironmentColor = "#000" };
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.ResolveCode("ANY")).Returns(fallbackInfo);
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.Same(fallbackInfo, controller.ResolveCode("ANY"));
    }

    /// <summary>The default implementation offers a single "Normal" priority level, tags enabled with label "Tag", and no blocked combinations.</summary>
    [Fact]
    public void DefaultEngineController_ReturnsHardcodedMessageCompositionDefaults()
    {
        TestEngineController controller = new();

        IReadOnlyList<MessagePriorityOption> priorities = controller.Priorities;
        Assert.Equal(["Normal"], priorities.Select(p => p.Name).ToList());
        Assert.Equal([0], priorities.Select(p => p.Value).ToList());
        Assert.True(controller.TagsEnabled);
        Assert.Equal("Tag", controller.TagLabel);
        Assert.Empty(controller.BlockedCombinations);
    }

    /// <summary>Priorities returns the same list instance/values on every access.</summary>
    [Fact]
    public void DefaultEngineController_Priorities_IsStableAcrossCalls()
    {
        TestEngineController controller = new();
        Assert.Equal(controller.Priorities, controller.Priorities);
    }

    /// <summary>Falls back to the wrapped provider for TagsEnabled/TagLabel when not configured; Priorities/BlockedCombinations always delegate.</summary>
    [Fact]
    public void ConfiguredEngineController_FallsBackWhenMessageCompositionNotConfigured()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.TagsEnabled).Returns(false);
        fallback.Setup(f => f.TagLabel).Returns("Category");
        IReadOnlyList<MessagePriorityOption> priorities = [new MessagePriorityOption { Name = "Low", Value = 0 }];
        fallback.Setup(f => f.Priorities).Returns(priorities);
        IReadOnlyList<TagPriorityBlock> blocks = [new TagPriorityBlock { Tag = "SPAM" }];
        fallback.Setup(f => f.BlockedCombinations).Returns(blocks);
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.False(controller.TagsEnabled);
        Assert.Equal("Category", controller.TagLabel);
        Assert.Same(priorities, controller.Priorities);
        Assert.Same(blocks, controller.BlockedCombinations);
    }

    /// <summary>TagsEnabled reflects an explicit false override from config.</summary>
    [Fact]
    public void ConfiguredEngineController_ReturnsFalseWhenTagsDisabledInConfig()
    {
        ConfiguredEngineController controller = new(new TestEngineController(), new EngineConfig { MessageTagsEnabled = false }, NoCurrentUser);
        Assert.False(controller.TagsEnabled);
    }

    /// <summary>TagLabel reflects an explicit override from config.</summary>
    [Fact]
    public void ConfiguredEngineController_TagLabel_ReturnsConfiguredValue()
    {
        ConfiguredEngineController controller = new(new TestEngineController(), new EngineConfig { MessageTagLabel = "Category" }, NoCurrentUser);
        Assert.Equal("Category", controller.TagLabel);
    }

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

    /// <summary>The default implementation returns hardcoded settings.</summary>
    [Fact]
    public void DefaultEngineController_ReturnsHardcodedAlertDefaults()
    {
        TestEngineController controller = new();
        Assert.Equal("ALERT", controller.AlertLabel);
        Assert.Equal(TimeSpan.FromSeconds(30), controller.AlarmSoundDuration);
        Assert.True(controller.QuickConfirmationEnabled);
        Assert.True(controller.ComposeAlertsEnabled);
    }

    /// <summary>Falls back to the wrapped provider for every field when not configured.</summary>
    [Fact]
    public void ConfiguredEngineController_FallsBackWhenAlertSettingsNotConfigured()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.AlertLabel).Returns("FALLBACK");
        fallback.Setup(f => f.AlarmSoundDuration).Returns(TimeSpan.FromSeconds(12));
        fallback.Setup(f => f.QuickConfirmationEnabled).Returns(false);
        fallback.Setup(f => f.ComposeAlertsEnabled).Returns(false);
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.Equal("FALLBACK", controller.AlertLabel);
        Assert.Equal(TimeSpan.FromSeconds(12), controller.AlarmSoundDuration);
        Assert.False(controller.QuickConfirmationEnabled);
        Assert.False(controller.ComposeAlertsEnabled);
    }

    /// <summary>Every settable field reflects an explicit override from config.</summary>
    [Fact]
    public void ConfiguredEngineController_OverridesAlertSettingsFromConfig()
    {
        ConfiguredEngineController controller = new(new TestEngineController(), new EngineConfig
        {
            AlertText = "URGENT",
            AlarmSoundSeconds = 5,
            QuickConfirmationEnabled = false,
            ComposeAlertsEnabled = false
        }, NoCurrentUser);

        Assert.Equal("URGENT", controller.AlertLabel);
        Assert.Equal(TimeSpan.FromSeconds(5), controller.AlarmSoundDuration);
        Assert.False(controller.QuickConfirmationEnabled);
        Assert.False(controller.ComposeAlertsEnabled);
    }

    /// <summary>The default implementation is disabled by default and prints every message exactly once.</summary>
    [Fact]
    public void DefaultEngineController_ReturnsHardcodedPrintPolicyDefaults()
    {
        TestEngineController controller = new();
        Assert.False(controller.PrintReceivedDefaultEnabled);
        Assert.Equal(1, controller.GetPrintCount(new TestMessage()));
    }

    /// <summary>Falls back to the wrapped provider when not configured.</summary>
    [Fact]
    public void ConfiguredEngineController_FallsBackWhenPrintPolicyNotConfigured()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.PrintReceivedDefaultEnabled).Returns(true);
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.True(controller.PrintReceivedDefaultEnabled);
    }

    /// <summary>PrintReceivedDefaultEnabled reflects an explicit true override from config.</summary>
    [Fact]
    public void ConfiguredEngineController_ReturnsTruePrintPolicyWhenEnabledInConfig()
    {
        ConfiguredEngineController controller = new(new TestEngineController(), new EngineConfig { PrintReceivedEnabled = true }, NoCurrentUser);
        Assert.True(controller.PrintReceivedDefaultEnabled);
    }

    /// <summary>GetPrintCount always delegates to the wrapped provider, since there is no corresponding config.json field.</summary>
    [Fact]
    public void ConfiguredEngineController_GetPrintCount_AlwaysDelegatesToFallback()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.GetPrintCount(It.IsAny<object>())).Returns(5);
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.Equal(5, controller.GetPrintCount(new object()));
    }

    /// <summary>The default implementation always returns the auto-detected name.</summary>
    [Fact]
    public void DefaultEngineController_GetCertificateName_AlwaysReturnsAutoName()
    {
        TestEngineController controller = new();
        Assert.Equal("USER-ALPHA", controller.GetCertificateName("ALPHA"));
    }

    /// <summary>Null config falls back to the wrapped provider.</summary>
    [Fact]
    public void ConfiguredEngineController_NullPeerCertificateNameConfig_FallsBack()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.GetCertificateName("ALPHA")).Returns("FALLBACK-NAME");
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.Equal("FALLBACK-NAME", controller.GetCertificateName("ALPHA"));
    }

    /// <summary>"disable" config → null (no authentication), regardless of the wrapped provider.</summary>
    [Fact]
    public void ConfiguredEngineController_DisablePeerCertificateNameConfig_ReturnsNull()
    {
        ConfiguredEngineController controller = new(new TestEngineController(), new EngineConfig { PeerCertificateName = "disable" }, NoCurrentUser);
        Assert.Null(controller.GetCertificateName("ALPHA"));
    }

    /// <summary>Explicit name → that name regardless of user.</summary>
    [Fact]
    public void ConfiguredEngineController_ExplicitPeerCertificateNameConfig_ReturnsExactName()
    {
        ConfiguredEngineController controller = new(new TestEngineController(), new EngineConfig { PeerCertificateName = "MY-CERT" }, NoCurrentUser);
        Assert.Equal("MY-CERT", controller.GetCertificateName("ALPHA"));
        Assert.Equal("MY-CERT", controller.GetCertificateName("BETA"));
    }

    /// <summary>ConnectionOptions returns an unauthenticated (Secure) policy when no current user is installed.</summary>
    [Fact]
    public void DefaultEngineController_ConnectionOptions_NoCurrentUser_ReturnsUnauthenticated()
    {
        TestEngineController controller = new();
        OftPeerOptions options = controller.ConnectionOptions;

        Assert.Null(options.Certificate);
        Assert.Equal(OftSecurityMode.Secure, options.SecurityMode);
    }

    /// <summary>The default implementation always returns the well-known default ports.</summary>
    [Fact]
    public void DefaultEngineController_UsesDefaultPorts()
    {
        TestEngineController controller = new();
        Assert.Equal(50021, controller.PeerPort);
        Assert.Equal(50020, controller.InterfacePort);
    }

    /// <summary>Null ports fall back to the wrapped provider.</summary>
    [Fact]
    public void ConfiguredEngineController_NullPorts_FallsBack()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.PeerPort).Returns(11111);
        fallback.Setup(f => f.InterfacePort).Returns(22222);
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.Equal(11111, controller.PeerPort);
        Assert.Equal(22222, controller.InterfacePort);
    }

    /// <summary>Configured ports override the fallback.</summary>
    [Fact]
    public void ConfiguredEngineController_ConfiguredPorts_OverrideFallback()
    {
        ConfiguredEngineController controller = new(new TestEngineController(), new EngineConfig { PeerPort = 9001, InterfacePort = 9002 }, NoCurrentUser);
        Assert.Equal(9001, controller.PeerPort);
        Assert.Equal(9002, controller.InterfacePort);
    }

    /// <summary>The default implementation never knows about any user, group, or name.</summary>
    [Fact]
    public void DefaultEngineController_UserDirectoryAlwaysEmpty()
    {
        TestEngineController controller = new();
        Assert.Null(controller.GetEndpoint("ANY"));
        Assert.Empty(controller.UserGroups);
        Assert.Empty(controller.Users);
    }

    /// <summary>Returns endpoint for a configured user.</summary>
    [Fact]
    public void ConfiguredEngineController_KnownUser_ReturnsEndpoint()
    {
        EngineConfig config = new()
        {
            Users = new Dictionary<string, UserEndpointConfig>
            {
                ["ALPHA"] = new UserEndpointConfig { IpAddress = "192.168.1.10", Port = 7890 }
            }
        };
        ConfiguredEngineController controller = new(new TestEngineController(), config, NoCurrentUser);

        UserEndpoint? result = controller.GetEndpoint("ALPHA");

        Assert.NotNull(result);
        Assert.Equal("192.168.1.10", result.IpAddress);
        Assert.Equal(7890, result.Port);
    }

    /// <summary>Falls back to the wrapped provider for an unknown user.</summary>
    [Fact]
    public void ConfiguredEngineController_UnknownUser_FallsBack()
    {
        UserEndpoint fallbackEndpoint = new() { IpAddress = "10.0.0.1", Port = 1 };
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.GetEndpoint("UNKNOWN")).Returns(fallbackEndpoint);
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.Same(fallbackEndpoint, controller.GetEndpoint("UNKNOWN"));
    }

    /// <summary>Config groups are merged over the fallback's own groups, config winning on key conflicts.</summary>
    [Fact]
    public void ConfiguredEngineController_MergesGroupsConfigOverFallback()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.UserGroups).Returns(
            new Dictionary<string, IReadOnlyList<string>> { ["OPS"] = ["FALLBACK-USER"], ["ONLY-FALLBACK"] = ["X"] });

        EngineConfig config = new()
        {
            UserGroups = new Dictionary<string, List<string>> { ["OPS"] = ["ALPHA", "BETA"] }
        };
        ConfiguredEngineController controller = new(fallback.Object, config, NoCurrentUser);

        IReadOnlyDictionary<string, IReadOnlyList<string>> groups = controller.UserGroups;

        Assert.Equal(["ALPHA", "BETA"], groups["OPS"]);
        Assert.Equal(["X"], groups["ONLY-FALLBACK"]);
    }

    /// <summary>Combines the fallback's names with configured user and group names, deduplicates, and sorts alphabetically.</summary>
    [Fact]
    public void ConfiguredEngineController_CombinesFallbackUsersAndGroupNames()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.Users).Returns((IReadOnlyList<string>)["DELTA"]);

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
        ConfiguredEngineController controller = new(fallback.Object, config, NoCurrentUser);

        IReadOnlyList<string> names = controller.Users;

        Assert.Equal(["ALPHA", "BRAVO", "CHARLIE", "DELTA"], names);
    }

    /// <summary>Empty config produces just the fallback's names.</summary>
    [Fact]
    public void ConfiguredEngineController_EmptyConfig_ReturnsFallbackUserNamesOnly()
    {
        ConfiguredEngineController controller = new(new TestEngineController(), new EngineConfig(), NoCurrentUser);
        Assert.Empty(controller.Users);
    }

    /// <summary>The default implementation is always Peer with no server endpoint or server users configured.</summary>
    [Fact]
    public void DefaultEngineController_PeerWithNothingConfigured()
    {
        TestEngineController controller = new();
        Assert.Equal(NodeRole.Peer, controller.Role);
        Assert.Null(controller.ServerEndpoint);
        Assert.Empty(controller.Servers);
    }

    /// <summary>Falls back to the wrapped provider when config does not set a role.</summary>
    [Fact]
    public void ConfiguredEngineController_FallsBackWhenRoleNotConfigured()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.Role).Returns(NodeRole.Server);
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.Equal(NodeRole.Server, controller.Role);
    }

    /// <summary>An unrecognized config role string falls back to the wrapped provider.</summary>
    [Fact]
    public void ConfiguredEngineController_UnrecognizedRole_FallsBack()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.Role).Returns(NodeRole.Client);
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig { NodeRole = "Bogus" }, NoCurrentUser);

        Assert.Equal(NodeRole.Client, controller.Role);
    }

    /// <summary>A recognized config role overrides the fallback.</summary>
    [Fact]
    public void ConfiguredEngineController_RecognizedRole_Overrides()
    {
        ConfiguredEngineController controller = new(new TestEngineController(), new EngineConfig { NodeRole = "Server" }, NoCurrentUser);
        Assert.Equal(NodeRole.Server, controller.Role);
    }

    /// <summary>Falls back to the wrapped provider when config does not set an endpoint.</summary>
    [Fact]
    public void ConfiguredEngineController_FallsBackWhenServerEndpointNotConfigured()
    {
        UserEndpoint fallbackEndpoint = new() { IpAddress = "10.0.0.1", Port = 1 };
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.ServerEndpoint).Returns(fallbackEndpoint);
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.Same(fallbackEndpoint, controller.ServerEndpoint);
    }

    /// <summary>A configured endpoint overrides the fallback.</summary>
    [Fact]
    public void ConfiguredEngineController_ServerEndpointOverridesFromConfig()
    {
        EngineConfig config = new() { ServerEndpoint = new UserEndpointConfig { IpAddress = "10.0.0.5", Port = 9000 } };
        ConfiguredEngineController controller = new(new TestEngineController(), config, NoCurrentUser);

        UserEndpoint? result = controller.ServerEndpoint;

        Assert.NotNull(result);
        Assert.Equal("10.0.0.5", result.IpAddress);
        Assert.Equal(9000, result.Port);
    }

    /// <summary>Config server users are merged over the fallback's own, config winning on key conflicts.</summary>
    [Fact]
    public void ConfiguredEngineController_MergesServerUsersConfigOverFallback()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.Servers).Returns(
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
        ConfiguredEngineController controller = new(fallback.Object, config, NoCurrentUser);

        IReadOnlyDictionary<string, ServerUserConfig> servers = controller.Servers;

        Assert.Equal("9.9.9.9", servers["SERVER-A"].Endpoint.IpAddress);
        Assert.Equal(["CONFIG-CHILD"], servers["SERVER-A"].ChildClients);
        Assert.Equal("2.2.2.2", servers["ONLY-FALLBACK"].Endpoint.IpAddress);
    }

    /// <summary>The default implementation always disables config file reading.</summary>
    [Fact]
    public void DefaultEngineController_ConfigFileDisabledByDefault()
    {
        TestEngineController controller = new();
        Assert.False(controller.ConfigFileEnabled);
    }

    /// <summary>ConfigFileEnabled has no config.json field and always delegates to the wrapped provider.</summary>
    [Fact]
    public void ConfiguredEngineController_ConfigFileEnabled_AlwaysDelegatesToFallback()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.ConfigFileEnabled).Returns(false);
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.False(controller.ConfigFileEnabled);
    }
}
