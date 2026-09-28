namespace BlueHeighliner.Comlink.Tests.Unit.Control;

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

    /// <summary>The default AppVersion is the entry assembly's major.minor.build version, and a host can override it.</summary>
    [Fact]
    public void DefaultEngineController_AppVersion_IsThreePartVersionAndOverridable()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", new TestEngineController().AppVersion);
        Assert.Equal("4.5.6", new TestAppVersionOverride().AppVersion);
    }

    private sealed class TestAppVersionOverride : TestEngineController
    {
        public override string AppVersion => "4.5.6";
    }

    /// <summary>The default NetworkSerializer is the protobuf-net implementation, and a host can override it entirely.</summary>
    [Fact]
    public void DefaultEngineController_NetworkSerializer_DefaultsToProtobufAndIsOverridable()
    {
        Assert.IsType<ProtobufNetworkSerializer>(new TestEngineController().NetworkSerializer);
        Assert.IsType<TestNetworkSerializer>(new TestNetworkSerializerOverride().NetworkSerializer);
    }

    /// <summary>An engine controller with one generic parameter has no packet type, serializer or packet fields, so nothing is packetized.</summary>
    [Fact]
    public void DefaultEngineController_WithoutPacketType_DoesNotPacketize()
    {
        IEngineController controller = new TestEngineController();

        Assert.Null(controller.PacketType);
        Assert.Null(controller.PacketSerializer);
        Assert.Throws<NotSupportedException>(() => controller.CreatePacket());
        Assert.Throws<NotSupportedException>(() => controller.GetPacketIndex(new object()));
    }

    /// <summary>A controller with a packet type reports it, defaults to the protobuf packet serializer, and can override that.</summary>
    [Fact]
    public void PacketEngineController_ReportsPacketTypeAndSerializer()
    {
        IEngineController controller = new TestPacketEngineController();
        IEngineController custom = new TestPacketSerializerOverride();

        Assert.Equal(typeof(TestPacket), controller.PacketType);
        Assert.IsType<ProtobufNetworkSerializer>(controller.PacketSerializer);
        Assert.IsType<TestNetworkSerializer>(custom.PacketSerializer);
        Assert.IsType<TestPacket>(controller.CreatePacket());
    }

    /// <summary>The packet field members get and set the fields of the host's packet type.</summary>
    [Fact]
    public void PacketEngineController_PacketFields_RoundTripThroughTheController()
    {
        IEngineController controller = new TestPacketEngineController();
        object packet = controller.CreatePacket();

        controller.SetPayloadId(packet, 7);
        controller.SetPacketIndex(packet, 2);
        controller.SetPacketCount(packet, 5);
        controller.SetPayloadLength(packet, 99);
        controller.SetPacketData(packet, new byte[] { 1, 2, 3 });

        Assert.Equal(7, controller.GetPayloadId(packet));
        Assert.Equal(2, controller.GetPacketIndex(packet));
        Assert.Equal(5, controller.GetPacketCount(packet));
        Assert.Equal(99, controller.GetPayloadLength(packet));
        Assert.Equal(new byte[] { 1, 2, 3 }, controller.GetPacketData(packet).ToArray());
        Assert.Equal(new byte[] { 1, 2, 3 }, ((TestPacket)packet).Data);
    }

    /// <summary>The default packet size is 16 KiB and the default window is 1, and a host can set both.</summary>
    [Fact]
    public void DefaultEngineController_PacketSizeAndWindow_HaveDefaultsAndAreOverridable()
    {
        TestEngineController defaults = new();
        TestPacketSizeOverride overridden = new();

        Assert.Equal(16 * 1024, defaults.PacketSize);
        Assert.Equal(1, defaults.PacketWindow);
        Assert.Equal(512, overridden.PacketSize);
        Assert.Equal(4, overridden.PacketWindow);
    }

    private sealed class TestPacketSizeOverride : TestPacketEngineController
    {
        public override int PacketSize => 512;
        public override int PacketWindow => 4;
    }

    private sealed class TestPacketSerializerOverride : TestPacketEngineController
    {
        public override INetworkSerializer PacketSerializer { get; } = new TestNetworkSerializer();
    }

    private sealed class TestNetworkSerializer : INetworkSerializer
    {
        public IMemoryOwner<byte> Serialize(object value) => throw new NotSupportedException();
        public object? Deserialize(ReadOnlyMemory<byte> data) => throw new NotSupportedException();
    }

    private sealed class TestNetworkSerializerOverride : TestEngineController
    {
        public override INetworkSerializer NetworkSerializer { get; } = new TestNetworkSerializer();
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
        fallback.Setup(f => f.AppVersion).Returns("9.8.7");
        fallback.Setup(f => f.IsKioskMode).Returns(true);
        fallback.Setup(f => f.HomeText).Returns("FALLBACK-HOME");
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.Equal("FallbackApp", controller.AppName);
        Assert.Equal("9.8.7", controller.AppVersion);
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

    /// <summary>The default implementation allows deletion in every root folder type.</summary>
    [Fact]
    public void DefaultEngineController_CanDelete_DefaultsToTrueForEveryFolderType()
    {
        TestEngineController controller = new();
        foreach (FolderType folderType in Enum.GetValues<FolderType>())
        {
            Assert.True(controller.CanDelete(folderType));
        }
    }

    /// <summary>CanDelete always delegates to the wrapped provider, since there is no corresponding config.json field.</summary>
    [Fact]
    public void ConfiguredEngineController_CanDelete_AlwaysDelegatesToFallback()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.CanDelete(FolderType.Drafts)).Returns(false);
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.False(controller.CanDelete(FolderType.Drafts));
    }

    /// <summary>The default implementation returns the user name unchanged, with no prefix.</summary>
    [Fact]
    public void DefaultEngineController_GetCertificateName_AlwaysReturnsAutoName()
    {
        TestEngineController controller = new();
        Assert.Equal("ALPHA", controller.GetCertificateName("ALPHA"));
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

    /// <summary>An explicit name names this node's own certificate, so it applies to the current user only; every other user keeps the wrapped provider's name, which is what a Server matches connecting certificates against.</summary>
    [Fact]
    public void ConfiguredEngineController_ExplicitPeerCertificateNameConfig_AppliesToCurrentUserOnly()
    {
        CurrentUserProvider currentUser = new() { UserName = "alpha" };
        ConfiguredEngineController controller = new(new TestEngineController(), new EngineConfig { PeerCertificateName = "MY-CERT" }, currentUser);

        Assert.Equal("MY-CERT", controller.GetCertificateName("ALPHA"));
        Assert.Equal("BETA", controller.GetCertificateName("BETA"));
    }

    /// <summary>ConnectionOptions throws when no current user is installed, since MSMT peer authentication is mandatory and there is no user to resolve an identity certificate for.</summary>
    [Fact]
    public void DefaultEngineController_ConnectionOptions_NoCurrentUser_Throws()
    {
        TestEngineController controller = new();

        Assert.Throws<InvalidOperationException>(() => controller.ConnectionOptions);
    }

    /// <summary>The default implementation always returns the well-known trusted authority name.</summary>
    [Fact]
    public void DefaultEngineController_TrustedAuthorityCertificateName_ReturnsDefaultName()
    {
        TestEngineController controller = new();
        Assert.Equal("COMLINK-ROOT", controller.TrustedAuthorityCertificateName);
    }

    /// <summary>Null config falls back to the wrapped provider.</summary>
    [Fact]
    public void ConfiguredEngineController_NullTrustedAuthorityCertificateNameConfig_FallsBack()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.TrustedAuthorityCertificateName).Returns("FALLBACK-ROOT");
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.Equal("FALLBACK-ROOT", controller.TrustedAuthorityCertificateName);
    }

    /// <summary>Explicit name → that name, regardless of the wrapped provider.</summary>
    [Fact]
    public void ConfiguredEngineController_ExplicitTrustedAuthorityCertificateNameConfig_ReturnsExactName()
    {
        ConfiguredEngineController controller = new(new TestEngineController(), new EngineConfig { TrustedAuthorityCertificateName = "MY-ROOT" }, NoCurrentUser);
        Assert.Equal("MY-ROOT", controller.TrustedAuthorityCertificateName);
    }

    /// <summary>When both certificate file fields are set, ConnectionOptions loads the identity and trusted authority directly from disk instead of the system store.</summary>
    [Fact]
    public void ConfiguredEngineController_BothCertificateFilesSet_LoadsFromFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"comlink-cert-file-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            (X509Certificate2 server, X509Certificate2 client, X509Certificate2Collection trustedAuthorities) = TestMsmtCertificates.Create();
            string peerFile = Path.Combine(tempDir, "identity.pfx");
            string authorityFile = Path.Combine(tempDir, "authority.cer");
            File.WriteAllBytes(peerFile, client.Export(X509ContentType.Pfx));
            File.WriteAllBytes(authorityFile, trustedAuthorities[0].Export(X509ContentType.Cert));

            ConfiguredEngineController controller = new(
                new TestEngineController(),
                new EngineConfig { PeerCertificateFile = peerFile, TrustedAuthorityCertificateFile = authorityFile },
                NoCurrentUser);

            MsmtSessionPeerOptions options = controller.ConnectionOptions;

            Assert.Equal(client.Thumbprint, options.Credentials.Identity.Thumbprint);
            Assert.Single(options.Credentials.TrustedAuthorities);
            Assert.Equal(trustedAuthorities[0].Thumbprint, options.Credentials.TrustedAuthorities[0].Thumbprint);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>Setting only PeerCertificateFile without TrustedAuthorityCertificateFile throws, since the two must be set together.</summary>
    [Fact]
    public void ConfiguredEngineController_OnlyPeerCertificateFileSet_Throws()
    {
        ConfiguredEngineController controller = new(new TestEngineController(), new EngineConfig { PeerCertificateFile = "/tmp/identity.pfx" }, NoCurrentUser);
        Assert.Throws<InvalidOperationException>(() => controller.ConnectionOptions);
    }

    /// <summary>Setting only TrustedAuthorityCertificateFile without PeerCertificateFile throws, since the two must be set together.</summary>
    [Fact]
    public void ConfiguredEngineController_OnlyTrustedAuthorityCertificateFileSet_Throws()
    {
        ConfiguredEngineController controller = new(new TestEngineController(), new EngineConfig { TrustedAuthorityCertificateFile = "/tmp/authority.cer" }, NoCurrentUser);
        Assert.Throws<InvalidOperationException>(() => controller.ConnectionOptions);
    }

    /// <summary>A configured identity certificate file that does not exist on disk throws.</summary>
    [Fact]
    public void ConfiguredEngineController_PeerCertificateFileMissing_Throws()
    {
        ConfiguredEngineController controller = new(
            new TestEngineController(),
            new EngineConfig { PeerCertificateFile = "/nonexistent/identity.pfx", TrustedAuthorityCertificateFile = "/nonexistent/authority.cer" },
            NoCurrentUser);

        Assert.Throws<InvalidOperationException>(() => controller.ConnectionOptions);
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
        Assert.Empty(controller.GetUserData("ANY"));
        Assert.Empty(controller.UserGroups);
        Assert.Empty(controller.Users);
    }

    /// <summary>A configured user's data is returned for that user.</summary>
    [Fact]
    public void ConfiguredEngineController_KnownUser_ReturnsConfiguredData()
    {
        EngineConfig config = new()
        {
            Users = new Dictionary<string, UserConfig>
            {
                ["ALPHA"] = new UserConfig { Data = new Dictionary<string, string> { ["role"] = "clerk" } }
            }
        };
        ConfiguredEngineController controller = new(new TestEngineController(), config, NoCurrentUser);

        IReadOnlyDictionary<string, string> result = controller.GetUserData("alpha");

        Assert.Equal("clerk", result["role"]);
    }

    /// <summary>Configured data is merged over the wrapped provider's own data for the same user, config winning on conflicts.</summary>
    [Fact]
    public void ConfiguredEngineController_UserData_MergesConfigOverFallback()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.GetUserData("ALPHA")).Returns(new Dictionary<string, string> { ["role"] = "fallback", ["desk"] = "4" });
        EngineConfig config = new()
        {
            Users = new Dictionary<string, UserConfig> { ["ALPHA"] = new UserConfig { Data = new Dictionary<string, string> { ["role"] = "config" } } }
        };
        ConfiguredEngineController controller = new(fallback.Object, config, NoCurrentUser);

        IReadOnlyDictionary<string, string> result = controller.GetUserData("ALPHA");

        Assert.Equal("config", result["role"]);
        Assert.Equal("4", result["desk"]);
    }

    /// <summary>Falls back to the wrapped provider's data for an unconfigured user.</summary>
    [Fact]
    public void ConfiguredEngineController_UnknownUser_FallsBackToFallbackData()
    {
        IReadOnlyDictionary<string, string> fallbackData = new Dictionary<string, string> { ["k"] = "v" };
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.GetUserData("UNKNOWN")).Returns(fallbackData);
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.Same(fallbackData, controller.GetUserData("UNKNOWN"));
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
            Users = new Dictionary<string, UserConfig>
            {
                ["CHARLIE"] = new UserConfig(),
                ["ALPHA"] = new UserConfig()
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

    /// <summary>The default implementation is always Peer with no outgoing points or server users configured.</summary>
    [Fact]
    public void DefaultEngineController_PeerWithNothingConfigured()
    {
        TestEngineController controller = new();
        Assert.Equal(NodeRole.Peer, controller.Role);
        Assert.Empty(controller.OutgoingPoints);
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

    /// <summary>Falls back to the wrapped provider when config lists no outgoing points.</summary>
    [Fact]
    public void ConfiguredEngineController_FallsBackWhenOutgoingPointsNotConfigured()
    {
        IReadOnlyList<ConnectionPoint> fallbackPoints = [new ConnectionPoint { IpAddress = "10.0.0.1", Port = 1 }];
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.OutgoingPoints).Returns(fallbackPoints);
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.Same(fallbackPoints, controller.OutgoingPoints);
    }

    /// <summary>Configured outgoing points replace the fallback's.</summary>
    [Fact]
    public void ConfiguredEngineController_OutgoingPointsOverrideFromConfig()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.OutgoingPoints).Returns([new ConnectionPoint { IpAddress = "10.0.0.1", Port = 1 }]);
        EngineConfig config = new() { OutgoingPoints = [new ConnectionPointConfig { IpAddress = "10.0.0.5", Port = 9000 }] };
        ConfiguredEngineController controller = new(fallback.Object, config, NoCurrentUser);

        ConnectionPoint point = Assert.Single(controller.OutgoingPoints);

        Assert.Equal("10.0.0.5", point.IpAddress);
        Assert.Equal(9000, point.Port);
    }

    /// <summary>Config server users are merged over the fallback's own, config winning on key conflicts.</summary>
    [Fact]
    public void ConfiguredEngineController_MergesServerUsersConfigOverFallback()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.Servers).Returns(
            new Dictionary<string, ServerUserConfig>
            {
                ["SERVER-A"] = new ServerUserConfig { ChildClients = ["FALLBACK-CHILD"] },
                ["ONLY-FALLBACK"] = new ServerUserConfig { ChildClients = ["OTHER"] }
            });

        EngineConfig config = new()
        {
            ServerUsers = new Dictionary<string, ServerUserConfigEntry>
            {
                ["SERVER-A"] = new ServerUserConfigEntry { ChildClients = ["CONFIG-CHILD"] }
            }
        };
        ConfiguredEngineController controller = new(fallback.Object, config, NoCurrentUser);

        IReadOnlyDictionary<string, ServerUserConfig> servers = controller.Servers;

        Assert.Equal(["CONFIG-CHILD"], servers["SERVER-A"].ChildClients);
        Assert.Equal(["OTHER"], servers["ONLY-FALLBACK"].ChildClients);
    }

    /// <summary>By default no connection is identified by the controller (the engine decides), and no connection message is configured.</summary>
    [Fact]
    public void DefaultEngineController_NoConnectionHooks()
    {
        TestEngineController controller = new();
        ConnectionInfo connection = new() { Host = "10.0.0.1" };

        Assert.Null(controller.IdentifyConnection(connection));
        Assert.Null(controller.ConnectionMessageType);
        Assert.Null(controller.ConnectionResponseType);
        Assert.Null(controller.ConnectionSerializer);
        Assert.Null(controller.CreateConnectionMessage(connection));
        Assert.Null(controller.CreateConnectionResponse(connection));
    }

    /// <summary>Naming a connection message type gives a serializer that builds only the message and response types.</summary>
    [Fact]
    public void DefaultEngineController_ConnectionSerializer_BuildsOnlyTheConfiguredTypes()
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.ConnectionMessageType).Returns(typeof(TestHello));
        controller.Setup(c => c.ConnectionResponseType).Returns(typeof(TestWelcome));

        INetworkSerializer serializer = controller.Object.ConnectionSerializer!;
        using IMemoryOwner<byte> hello = serializer.Serialize(new TestHello { Name = "A" });
        using IMemoryOwner<byte> welcome = serializer.Serialize(new TestWelcome { Station = 4 });

        Assert.Equal("A", Assert.IsType<TestHello>(serializer.Deserialize(hello.Memory)).Name);
        Assert.Equal(4, Assert.IsType<TestWelcome>(serializer.Deserialize(welcome.Memory)).Station);
        using IMemoryOwner<byte> foreign = new ProtobufNetworkSerializer().Serialize(new TestMessage());
        Assert.Null(serializer.Deserialize(foreign.Memory));
    }

    /// <summary>The connection hooks and types are not configurable from config.json and delegate to the wrapped provider.</summary>
    [Fact]
    public void ConfiguredEngineController_ConnectionHooks_DelegateToFallback()
    {
        ConnectionInfo connection = new() { Host = "10.0.0.1" };
        UserIdentity identity = new() { Name = "BOB" };
        object message = new();
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.IdentifyConnection(connection)).Returns(identity);
        fallback.Setup(f => f.CreateConnectionMessage(connection)).Returns(message);
        fallback.Setup(f => f.ConnectionMessageType).Returns(typeof(TestHello));
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.Same(identity, controller.IdentifyConnection(connection));
        Assert.Same(message, controller.CreateConnectionMessage(connection));
        Assert.Equal(typeof(TestHello), controller.ConnectionMessageType);
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

    /// <summary>The packet type, serializer and field members have no config.json field and delegate straight to the wrapped controller.</summary>
    [Fact]
    public void ConfiguredEngineController_PacketMembers_DelegateToFallback()
    {
        TestPacketEngineController fallback = new();
        ConfiguredEngineController controller = new(fallback, new EngineConfig(), NoCurrentUser);

        Assert.Equal(typeof(TestPacket), controller.PacketType);
        Assert.Same(fallback.PacketSerializer, controller.PacketSerializer);
        Assert.Equal(fallback.PacketSize, controller.PacketSize);
        Assert.Equal(fallback.PacketWindow, controller.PacketWindow);
        object packet = controller.CreatePacket();
        controller.SetPayloadId(packet, 9);
        controller.SetPacketIndex(packet, 1);
        controller.SetPacketCount(packet, 2);
        controller.SetPayloadLength(packet, 30);
        controller.SetPacketData(packet, new byte[] { 5 });
        Assert.Equal(9, controller.GetPayloadId(packet));
        Assert.Equal(1, controller.GetPacketIndex(packet));
        Assert.Equal(2, controller.GetPacketCount(packet));
        Assert.Equal(30, controller.GetPayloadLength(packet));
        Assert.Equal(new byte[] { 5 }, controller.GetPacketData(packet).ToArray());
    }

    /// <summary>Every message-field member has no config.json field and always delegates straight to the wrapped provider, working through the real TestMessage mapping.</summary>
    [Fact]
    public void ConfiguredEngineController_MessageFieldMembers_AlwaysDelegateToFallback()
    {
        TestEngineController fallback = new();
        ConfiguredEngineController controller = new(fallback, new EngineConfig(), NoCurrentUser);

        Assert.Equal(fallback.MessageType, controller.MessageType);
        Assert.Same(fallback.NetworkSerializer, controller.NetworkSerializer);
        Assert.Null(controller.PacketType);
        Assert.Null(controller.PacketSerializer);
        Assert.Equal(fallback.PacketSize, controller.PacketSize);
        Assert.Equal(fallback.PacketWindow, controller.PacketWindow);

        object message = controller.CreateMessage();
        Assert.IsType<TestMessage>(message);

        controller.SetMessageId(message, "M1");
        Assert.Equal("M1", controller.GetMessageId(message));
        controller.SetFromUser(message, "ALICE");
        Assert.Equal("ALICE", controller.GetFromUser(message));
        controller.SetSubject(message, "Hi");
        Assert.Equal("Hi", controller.GetSubject(message));
        controller.SetBody(message, "Body text");
        Assert.Equal("Body text", controller.GetBody(message));
        List<MessageAddress> addresses = [new MessageAddress { UserName = "BOB", Type = AddressType.To }];
        controller.SetAddresses(message, addresses);
        MessageAddress roundTripped = Assert.Single(controller.GetAddresses(message));
        Assert.Equal("BOB", roundTripped.UserName);
        Assert.Equal(AddressType.To, roundTripped.Type);
        DateTime sentAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        controller.SetSentAt(message, sentAt);
        Assert.Equal(sentAt, controller.GetSentAt(message));
        controller.SetConfirmationMessageId(message, "M0");
        Assert.Equal("M0", controller.GetConfirmationMessageId(message));
        controller.SetIsAlert(message, true);
        Assert.True(controller.GetIsAlert(message));
        controller.SetPriority(message, 2);
        Assert.Equal(2, controller.GetPriority(message));
        controller.SetTag(message, "URGENT");
        Assert.Equal("URGENT", controller.GetTag(message));
    }

    /// <summary>ExternalSystems has no config.json field and always delegates to the wrapped provider.</summary>
    [Fact]
    public void ConfiguredEngineController_ExternalSystems_AlwaysDelegatesToFallback()
    {
        Mock<IExternalSystem> externalSystem = new();
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.ExternalSystems).Returns([externalSystem.Object]);
        ConfiguredEngineController controller = new(fallback.Object, new EngineConfig(), NoCurrentUser);

        Assert.Same(externalSystem.Object, Assert.Single(controller.ExternalSystems));
    }

    /// <summary>With neither certificate file field configured, ConnectionOptions falls back to the system store lookup - and throws the same way DefaultEngineController does when no current user is registered.</summary>
    [Fact]
    public void ConfiguredEngineController_NoCertificateFilesConfigured_FallsBackToStoreLookup()
    {
        ConfiguredEngineController controller = new(new TestEngineController(), new EngineConfig(), NoCurrentUser);

        Assert.Throws<InvalidOperationException>(() => controller.ConnectionOptions);
    }

    /// <summary>The store-based lookup resolves a real identity certificate and trusted authority installed under their expected subject names in the current user's certificate store.</summary>
    [Fact]
    public void MsmtCertificateLookup_BuildPeerOptions_ResolvesFromSystemStore()
    {
        string identityName = $"comlink-test-identity-{Guid.NewGuid():N}";
        string authorityName = $"comlink-test-authority-{Guid.NewGuid():N}";
        using X509Certificate2 identityCert = SelfSignedCertificateNamed(identityName);
        using X509Certificate2 authorityCert = SelfSignedCertificateNamed(authorityName);

        using X509Store store = new(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Add(identityCert);
        store.Add(authorityCert);
        try
        {
            MsmtSessionPeerOptions options = MsmtCertificateLookup.BuildPeerOptions("ALPHA", _ => identityName, authorityName);

            Assert.Equal(identityCert.Thumbprint, options.Credentials.Identity.Thumbprint);
            Assert.Single(options.Credentials.TrustedAuthorities);
            Assert.Equal(authorityCert.Thumbprint, options.Credentials.TrustedAuthorities[0].Thumbprint);
        }
        finally
        {
            store.Remove(identityCert);
            store.Remove(authorityCert);
        }
    }

    /// <summary>The store-based lookup throws when the trusted authority certificate cannot be found, even though the identity certificate was.</summary>
    [Fact]
    public void MsmtCertificateLookup_BuildPeerOptions_AuthorityNotFound_Throws()
    {
        string identityName = $"comlink-test-identity-{Guid.NewGuid():N}";
        using X509Certificate2 identityCert = SelfSignedCertificateNamed(identityName);

        using X509Store store = new(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Add(identityCert);
        try
        {
            Assert.Throws<InvalidOperationException>(
                () => MsmtCertificateLookup.BuildPeerOptions("ALPHA", _ => identityName, "comlink-test-missing-authority"));
        }
        finally
        {
            store.Remove(identityCert);
        }
    }

    /// <summary>The file-based lookup throws when the authority file does not exist, even though the identity file does.</summary>
    [Fact]
    public void MsmtCertificateLookup_BuildPeerOptionsFromFiles_AuthorityFileMissing_Throws()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"comlink-cert-file-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            (_, X509Certificate2 client, _) = TestMsmtCertificates.Create();
            string peerFile = Path.Combine(tempDir, "identity.pfx");
            File.WriteAllBytes(peerFile, client.Export(X509ContentType.Pfx));

            Assert.Throws<InvalidOperationException>(
                () => MsmtCertificateLookup.BuildPeerOptionsFromFiles(peerFile, Path.Combine(tempDir, "missing-authority.cer")));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static X509Certificate2 SelfSignedCertificateNamed(string simpleName)
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new($"CN={simpleName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
    }
}
