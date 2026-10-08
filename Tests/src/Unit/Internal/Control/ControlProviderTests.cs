namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Control;

/// <summary>
/// Unit tests for <see cref="EngineController"/> (via the <see cref="TestEngineController"/> test double) and its corresponding <see cref="ConfiguredEngineController"/>
/// node settings decorator over the network configuration file.
/// </summary>
public sealed class ControlProviderTests
{
    private static string SystemAppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static ICurrentUserProvider NoCurrentUser => new CurrentUserProvider();
    private static ICurrentUserProvider Me => new CurrentUserProvider { UserName = "ME" };

    private static NetworkConfig Node(NetworkUserConfig me, string? user = null) => new() { User = user, Users = { ["ME"] = me } };

    /// <summary>The default implementation derives AppDataPath from AppName (there is no user) via virtual dispatch, and returns hardcoded defaults for everything else.</summary>
    [Fact]
    public void EngineController_UsesAppNameAndHardcodedDefaults()
    {
        TestEngineController controller = new();
        Assert.Equal(Path.Combine(SystemAppData, controller.AppName), controller.AppDataPath);
        Assert.False(controller.IsKioskMode);
        Assert.Equal("HOME", controller.HomeText);
    }

    /// <summary>The default AppVersion is the entry assembly's major.minor.build version, and a host can override it.</summary>
    [Fact]
    public void EngineController_AppVersion_IsThreePartVersionAndOverridable()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", new TestEngineController().AppVersion);
        Assert.Equal("4.5.6", new TestAppVersionOverride().AppVersion);
    }

    private sealed class TestAppVersionOverride : TestEngineController
    {
        public override string AppVersion => "4.5.6";
    }

    /// <summary>The default FrameSerializer is the protobuf-net implementation, and a host can override it entirely.</summary>
    [Fact]
    public void EngineController_NetworkSerializer_DefaultsToProtobufAndIsOverridable()
    {
        Assert.IsType<ProtobufSerializer>(new TestEngineController().FrameSerializer);
        Assert.IsType<TestNetworkSerializer>(new TestNetworkSerializerOverride().FrameSerializer);
    }

    /// <summary>An engine controller with one generic parameter has no packet type, serializer or packet fields, so nothing is packetized.</summary>
    [Fact]
    public void EngineController_WithoutPacketType_DoesNotPacketize()
    {
        IEngineController controller = new TestEngineController();

        Assert.Null(controller.PacketType);
        Assert.Null(controller.PacketSerializer);
        Assert.Throws<NotSupportedException>(() => controller.CreateFramePacket(new FramePacketCreateContext { PayloadId = 1, Index = 0, Count = 1, PayloadLength = 0, Data = ReadOnlyMemory<byte>.Empty }));
        Assert.Throws<NotSupportedException>(() => controller.GetPacketIndex(new object()));
    }

    /// <summary>A controller with a packet type reports it, defaults to the protobuf packet serializer, and can override that.</summary>
    [Fact]
    public void PacketEngineController_ReportsPacketTypeAndSerializer()
    {
        IEngineController controller = new TestPacketEngineController();
        IEngineController custom = new TestPacketSerializerOverride();

        Assert.Equal(typeof(TestPacket), controller.PacketType);
        Assert.IsType<ProtobufSerializer>(controller.PacketSerializer);
        Assert.IsType<TestPacketSerializer>(custom.PacketSerializer);
        Assert.IsType<TestPacket>(controller.CreateFramePacket(new FramePacketCreateContext { PayloadId = 1, Index = 0, Count = 1, PayloadLength = 0, Data = ReadOnlyMemory<byte>.Empty }));
    }

    /// <summary>Creating a frame packet through the controller and reading it back round-trips the fields of the host's packet type.</summary>
    [Fact]
    public void PacketEngineController_PacketFields_RoundTripThroughTheController()
    {
        IEngineController controller = new TestPacketEngineController();
        object packet = controller.CreateFramePacket(new FramePacketCreateContext { PayloadId = 7, Index = 2, Count = 5, PayloadLength = 99, Data = new byte[] { 1, 2, 3 } });

        Assert.True(controller.IsFramePacket(packet));

        Assert.Equal(7, controller.GetPayloadId(packet));
        Assert.Equal(2, controller.GetPacketIndex(packet));
        Assert.Equal(5, controller.GetPacketCount(packet));
        Assert.Equal(99, controller.GetPayloadLength(packet));
        Assert.Equal(new byte[] { 1, 2, 3 }, controller.GetPacketData(packet).ToArray());
        Assert.Equal(new byte[] { 1, 2, 3 }, ((TestPacket)packet).Data);
    }

    /// <summary>The default packet size is 16 KiB and the default window is 1, and a host can set both.</summary>
    [Fact]
    public void EngineController_PacketSizeAndWindow_HaveDefaultsAndAreOverridable()
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
        public override IPacketSerializer? PacketSerializer { get; } = new TestPacketSerializer();
    }

    private sealed class TestPacketSerializer : IPacketSerializer
    {
        public IMemoryOwner<byte> Serialize(object value, object? frame) => throw new NotSupportedException();
        public object Deserialize(ReadOnlyMemory<byte> data) => throw new NotSupportedException();
    }

    private sealed class TestNetworkSerializer : IFrameSerializer
    {
        public void ConfigurePacket(object frame, object packet) => throw new NotSupportedException();
        public IMemoryOwner<byte> Serialize(object value) => throw new NotSupportedException();
        public object Deserialize(ReadOnlyMemory<byte> data, object? packet) => throw new NotSupportedException();
    }

    private sealed class TestNetworkSerializerOverride : TestEngineController
    {
        public override IFrameSerializer FrameSerializer { get; } = new TestNetworkSerializer();
    }

    /// <summary>The app name is only a display name: changing it never moves the app data folder, which has a member of its own.</summary>
    [Fact]
    public void EngineController_OverridingAppName_DoesNotMoveTheAppDataFolder()
    {
        TestAppNameOverride controller = new();
        Assert.Equal("CustomApp", controller.AppName);
        Assert.DoesNotContain("CustomApp", controller.AppDataPath);
    }

    private sealed class TestAppNameOverride : TestEngineController
    {
        public override string AppName => "CustomApp";
    }

    /// <summary>Without a user the wrapped provider's own path is used; with a network user named on the command line before anyone is installed, their folder under the data root is (a name that is not a user of the network gets none); an installed user's folder is the wrapped provider's.</summary>
    [Fact]
    public void ConfiguredEngineController_AppDataPath_FollowsTheLaunchedUser()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.AppDataPath).Returns("/base/path");
        fallback.Setup(f => f.UserFilePath).Returns(Path.Combine("/base", "Data", "User.json"));
        fallback.Setup(f => f.FindUserName("alice")).Returns("ALICE");

        Assert.Equal("/base/path", new ConfiguredEngineController(fallback.Object, new NetworkConfig(), NoCurrentUser).AppDataPath);
        Assert.Equal(Path.Combine("/base", "Data", "ALICE"), new ConfiguredEngineController(fallback.Object, new NetworkConfig { User = "alice" }, NoCurrentUser).AppDataPath);
        Assert.Equal("/base/path", new ConfiguredEngineController(fallback.Object, new NetworkConfig { User = "nobody" }, NoCurrentUser).AppDataPath);
        Assert.Equal("/base/path", new ConfiguredEngineController(fallback.Object, new NetworkConfig { User = "alice" }, Me).AppDataPath);
    }

    /// <summary>AppName, IsKioskMode, and HomeText are left entirely to the fallback, since none has a network file field.</summary>
    [Fact]
    public void ConfiguredEngineController_NonConfigAppSettingsMembers_AlwaysDelegateToFallback()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.AppName).Returns("FallbackApp");
        fallback.Setup(f => f.AppVersion).Returns("9.8.7");
        fallback.Setup(f => f.IsKioskMode).Returns(true);
        fallback.Setup(f => f.HomeText).Returns("FALLBACK-HOME");
        ConfiguredEngineController controller = new(fallback.Object, new NetworkConfig(), NoCurrentUser);

        Assert.Equal("FallbackApp", controller.AppName);
        Assert.Equal("9.8.7", controller.AppVersion);
        Assert.True(controller.IsKioskMode);
        Assert.Equal("FALLBACK-HOME", controller.HomeText);
    }

    /// <summary>The default implementation has no debug override, and a user name is found only among the network's users.</summary>
    [Fact]
    public void EngineController_NoDebugOverride_FindsOnlyNetworkUsers()
    {
        TestEngineController controller = new();
        Assert.Null(controller.DebugUserName);

        Assert.Null(controller.FindUserName("CODE"));
        Assert.Null(controller.FindUserName("UNKNOWN"));
    }

    /// <summary>DebugUserName returns the value from config.</summary>
    [Fact]
    public void ConfiguredEngineController_ReturnsDebugUserNameFromConfig()
    {
        ConfiguredEngineController controller = new(new TestEngineController(), new NetworkConfig { User = "ALPHA" }, NoCurrentUser);
        Assert.Equal("ALPHA", controller.DebugUserName);
    }

    /// <summary>DebugUserName falls back to the wrapped provider when config has no override.</summary>
    [Fact]
    public void ConfiguredEngineController_FallsBackWhenDebugUserNameNotConfigured()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.DebugUserName).Returns("FALLBACK");
        ConfiguredEngineController controller = new(fallback.Object, new NetworkConfig(), NoCurrentUser);

        Assert.Equal("FALLBACK", controller.DebugUserName);
    }

    /// <summary>User lookup, the certificate check and user info are left entirely to the wrapped provider, since there is no corresponding network file field.</summary>
    [Fact]
    public void ConfiguredEngineController_UserResolution_AlwaysDelegatesToFallback()
    {
        UserInfo fallbackInfo = new() { Name = "X" };
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.FindUserName("ANY")).Returns("X");
        fallback.Setup(f => f.GetUserInfo("X")).Returns(fallbackInfo);
        ConfiguredEngineController controller = new(fallback.Object, new NetworkConfig(), NoCurrentUser);

        Assert.Equal("X", controller.FindUserName("ANY"));
        Assert.Same(fallbackInfo, controller.GetUserInfo("X"));
    }

    /// <summary>The default implementation offers a single "NORMAL" priority level, tags enabled with label "Tag", and no blocked combinations.</summary>
    [Fact]
    public void EngineController_ReturnsHardcodedMessageCompositionDefaults()
    {
        EngineController controller = new(EngineBuilder.Build(new TestEngineConfiguration()), new CurrentUserProvider(), null);

        IReadOnlyList<MessagePriorityOption> priorities = controller.Priorities;
        Assert.Equal(Enum.GetValues<TestMessagePriority>().Select(p => p.ToString().ToUpperInvariant()), priorities.Select(p => p.Name));
        Assert.Equal(Enumerable.Range(0, priorities.Count), priorities.Select(p => p.Value));
        Assert.True(controller.TagsEnabled);
        Assert.Equal("Tag", controller.TagLabel);
        Assert.Empty(controller.BlockedCombinations);
    }

    /// <summary>Priorities returns the same list instance/values on every access.</summary>
    [Fact]
    public void EngineController_Priorities_IsStableAcrossCalls()
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
        IReadOnlyList<MessagePriorityOption> priorities = [new MessagePriorityOption { Name = "Low", Value = 0, Key = TestMessagePriority.Low }];
        fallback.Setup(f => f.Priorities).Returns(priorities);
        IReadOnlyList<TagPriorityBlock> blocks = [new TagPriorityBlock { Tag = "SPAM" }];
        fallback.Setup(f => f.BlockedCombinations).Returns(blocks);
        ConfiguredEngineController controller = new(fallback.Object, new NetworkConfig(), NoCurrentUser);

        Assert.False(controller.TagsEnabled);
        Assert.Equal("Category", controller.TagLabel);
        Assert.Same(priorities, controller.Priorities);
        Assert.Same(blocks, controller.BlockedCombinations);
    }

    /// <summary>A rule with only Tag set blocks that tag regardless of priority.</summary>
    [Theory]
    [InlineData(TestMessagePriority.Normal)]
    [InlineData(TestMessagePriority.Level1)]
    [InlineData(TestMessagePriority.High)]
    public void TagPriorityBlockExtensions_IsBlocked_TagWithNullPriority_BlocksAnyPriority(TestMessagePriority priority)
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
        IReadOnlyList<TagPriorityBlock> blocks = [new TagPriorityBlock { Tag = null, Priority = TestMessagePriority.Level2 }];
        Assert.True(blocks.IsBlocked(tag, TestMessagePriority.Level2));
    }

    /// <summary>A rule with both fields set only blocks that exact tag/priority pair.</summary>
    [Fact]
    public void TagPriorityBlockExtensions_IsBlocked_SpecificPair_OnlyBlocksExactMatch()
    {
        IReadOnlyList<TagPriorityBlock> blocks = [new TagPriorityBlock { Tag = "URGENT", Priority = TestMessagePriority.Level2 }];

        Assert.True(blocks.IsBlocked("URGENT", TestMessagePriority.Level2));
        Assert.False(blocks.IsBlocked("URGENT", TestMessagePriority.Level1));
        Assert.False(blocks.IsBlocked("OTHER", TestMessagePriority.Level2));
    }

    /// <summary>Tag matching is case-insensitive.</summary>
    [Fact]
    public void TagPriorityBlockExtensions_IsBlocked_TagMatchIsCaseInsensitive()
    {
        IReadOnlyList<TagPriorityBlock> blocks = [new TagPriorityBlock { Tag = "SPAM", Priority = null }];
        Assert.True(blocks.IsBlocked("spam", TestMessagePriority.Normal));
    }

    /// <summary>No rule matches → not blocked.</summary>
    [Fact]
    public void TagPriorityBlockExtensions_IsBlocked_NoMatchingRule_ReturnsFalse()
    {
        IReadOnlyList<TagPriorityBlock> blocks = [new TagPriorityBlock { Tag = "SPAM", Priority = null }];
        Assert.False(blocks.IsBlocked("OK", TestMessagePriority.Normal));
    }

    /// <summary>Falls back to the wrapped provider when not configured.</summary>
    [Fact]
    public void ConfiguredEngineController_FallsBackWhenPrintPolicyNotConfigured()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.PrintReceivedDefaultEnabled).Returns(true);
        ConfiguredEngineController controller = new(fallback.Object, new NetworkConfig(), NoCurrentUser);

        Assert.True(controller.PrintReceivedDefaultEnabled);
    }

    /// <summary>The default implementation allows deletion in every root folder type.</summary>
    [Fact]
    public void EngineController_CanDelete_DefaultsToTrueForEveryFolderType()
    {
        TestEngineController controller = new();
        foreach (FolderType folderType in Enum.GetValues<FolderType>())
        {
            Assert.True(controller.CanDelete(folderType));
        }
    }

    /// <summary>CanDelete always delegates to the wrapped provider, since there is no corresponding network file field.</summary>
    [Fact]
    public void ConfiguredEngineController_CanDelete_AlwaysDelegatesToFallback()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.CanDelete(FolderType.Drafts)).Returns(false);
        ConfiguredEngineController controller = new(fallback.Object, new NetworkConfig(), NoCurrentUser);

        Assert.False(controller.CanDelete(FolderType.Drafts));
    }

    /// <summary>ConnectionOptions throws when no current user is installed, since MSMT peer authentication is mandatory and there is no user to resolve an identity certificate for.</summary>
    [Fact]
    public void EngineController_ConnectionOptions_NoCurrentUser_Throws()
    {
        TestEngineController controller = new();

        Assert.Throws<InvalidOperationException>(() => controller.ConnectionOptions);
    }

    /// <summary>When both the certificate store and the authority certificate are set, ConnectionOptions loads the current user's identity from {USERNAME}.pfx in the store and the authority from its file, </summary>
    [Fact]
    public void ConfiguredEngineController_StoreAndAuthoritySet_LoadsFromFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"comlink-cert-file-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            (X509Certificate2 server, X509Certificate2 client, X509Certificate2Collection trustedAuthorities) = TestMsmtCertificates.Create();
            string authorityFile = Path.Combine(tempDir, "authority.cer");
            File.WriteAllBytes(Path.Combine(tempDir, "ME.pfx"), client.Export(X509ContentType.Pfx));
            File.WriteAllBytes(authorityFile, trustedAuthorities[0].Export(X509ContentType.Cert));

            ConfiguredEngineController controller = new(
                new TestEngineController(),
                new NetworkConfig { AuthorityCertificate = authorityFile, CertificateStore = tempDir },
                Me);

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

    /// <summary>Setting only the certificate store without the authority certificate, or the reverse, throws, since the two must be set together.</summary>
    [Fact]
    public void ConfiguredEngineController_OnlyOneOfStoreAndAuthoritySet_Throws()
    {
        ConfiguredEngineController onlyStore = new(new TestEngineController(), new NetworkConfig { CertificateStore = "/tmp/store" }, Me);
        ConfiguredEngineController onlyAuthority = new(new TestEngineController(), new NetworkConfig { AuthorityCertificate = "/tmp/authority.cer" }, Me);

        Assert.Throws<InvalidOperationException>(() => onlyStore.ConnectionOptions);
        Assert.Throws<InvalidOperationException>(() => onlyAuthority.ConnectionOptions);
    }

    /// <summary>An identity certificate file missing from the store throws, and so does having no user to load one for.</summary>
    [Fact]
    public void ConfiguredEngineController_IdentityFileMissingOrNoUser_Throws()
    {
        NetworkConfig config = new() { AuthorityCertificate = "/nonexistent/authority.cer", CertificateStore = "/nonexistent" };

        Assert.Throws<InvalidOperationException>(() => new ConfiguredEngineController(new TestEngineController(), config, Me).ConnectionOptions);
        Assert.Throws<InvalidOperationException>(() => new ConfiguredEngineController(new TestEngineController(), config, NoCurrentUser).ConnectionOptions);
    }

    /// <summary>The default implementation always returns the well-known default ports.</summary>
    [Fact]
    public void EngineController_UsesDefaultPorts()
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
        ConfiguredEngineController controller = new(fallback.Object, new NetworkConfig(), NoCurrentUser);

        Assert.Equal(11111, controller.PeerPort);
        Assert.Equal(22222, controller.InterfacePort);
    }

    /// <summary>The default implementation never knows about any user, group, or name.</summary>
    [Fact]
    public void EngineController_UserDirectoryAlwaysEmpty()
    {
        TestEngineController controller = new();
        Assert.Empty(controller.GetUserData("ANY"));
        Assert.Empty(controller.UserGroups);
        Assert.Empty(controller.Users);
    }

    /// <summary>The default implementation is always Peer with no outgoing points or server users configured.</summary>
    [Fact]
    public void EngineController_ClientWithNothingConfigured()
    {
        TestEngineController controller = new();
        Assert.Equal(UserRole.Client, controller.Role);
        Assert.Empty(controller.OutgoingPoints);
        Assert.Empty(controller.Servers);
    }

    /// <summary>By default no initial exchange is configured.</summary>
    [Fact]
    public void EngineController_NoInitialExchange()
    {
        TestEngineController controller = new();

        Assert.Null(controller.InitialPacketProcessor);
        Assert.Null(controller.InitialFrameProcessor);
        Assert.Null(controller.NetworkHandler);
    }

    /// <summary>Every processor's connection description is told which user this node runs as, so what they send can say who is speaking.</summary>
    [Fact]
    public void ConfiguredEngineController_ConnectionDescriptions_SeeTheLocalUser()
    {
        IpConnectionInfo connection = new() { Host = "10.0.0.1" };
        Mock<IEngineController> fallback = new();
        ConfiguredEngineController controller = new(fallback.Object, new NetworkConfig(), Me);

        IpConnectionInfo expected = connection with { LocalUser = "ME" };
        Assert.Equal("ME", controller.WithLocalUser(connection).LocalUser);
        Assert.Equal(expected, controller.WithLocalUser(connection));
    }

    /// <summary>The initial exchange and network processors are not configurable from the network file and come from the wrapped provider.</summary>
    [Fact]
    public void ConfiguredEngineController_Processors_DelegateToFallback()
    {
        IInitialProcessor packets = Mock.Of<IInitialProcessor>();
        INetworkHandler network = Mock.Of<INetworkHandler>();
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.InitialPacketProcessor).Returns(packets);
        fallback.Setup(f => f.NetworkHandler).Returns(network);
        ConfiguredEngineController controller = new(fallback.Object, new NetworkConfig(), NoCurrentUser);

        Assert.Same(packets, controller.InitialPacketProcessor);
        Assert.Null(controller.InitialFrameProcessor);
        Assert.Same(network, controller.NetworkHandler);
    }

    /// <summary>The default implementation always disables config file reading.</summary>
    [Fact]
    public void EngineController_ConfigFileDisabledByDefault()
    {
        TestEngineController controller = new();
        Assert.False(controller.CommandLineOverridesAllowed);
    }

    /// <summary>CommandLineOverridesAllowed has no network file field and always delegates to the wrapped provider.</summary>
    [Fact]
    public void ConfiguredEngineController_CommandLineOverridesAllowed_AlwaysDelegatesToFallback()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.CommandLineOverridesAllowed).Returns(false);
        ConfiguredEngineController controller = new(fallback.Object, new NetworkConfig(), NoCurrentUser);

        Assert.False(controller.CommandLineOverridesAllowed);
    }

    /// <summary>The packet type, serializer and field members have no network file field and delegate straight to the wrapped controller.</summary>
    [Fact]
    public void ConfiguredEngineController_PacketMembers_DelegateToFallback()
    {
        TestPacketEngineController fallback = new();
        ConfiguredEngineController controller = new(fallback, new NetworkConfig(), NoCurrentUser);

        Assert.Equal(typeof(TestPacket), controller.PacketType);
        Assert.Same(fallback.PacketSerializer, controller.PacketSerializer);
        Assert.Equal(fallback.PacketSize, controller.PacketSize);
        Assert.Equal(fallback.PacketWindow, controller.PacketWindow);
        object packet = controller.CreateFramePacket(new FramePacketCreateContext { PayloadId = 9, Index = 1, Count = 2, PayloadLength = 30, Data = new byte[] { 5 } });
        Assert.Equal(9, controller.GetPayloadId(packet));
        Assert.Equal(1, controller.GetPacketIndex(packet));
        Assert.Equal(2, controller.GetPacketCount(packet));
        Assert.Equal(30, controller.GetPayloadLength(packet));
        Assert.Equal(new byte[] { 5 }, controller.GetPacketData(packet).ToArray());
    }

    /// <summary>ExternalSystems has no network file field and always delegates to the wrapped provider.</summary>
    [Fact]
    public void ConfiguredEngineController_ExternalSystems_AlwaysDelegatesToFallback()
    {
        Mock<IExternalSystem> externalSystem = new();
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.ExternalSystems).Returns([externalSystem.Object]);
        ConfiguredEngineController controller = new(fallback.Object, new NetworkConfig(), NoCurrentUser);

        Assert.Same(externalSystem.Object, Assert.Single(controller.ExternalSystems));
    }

    /// <summary>With neither certificate file field configured, ConnectionOptions falls back to the system store lookup - and throws the same way DefaultEngineController does when no current user is registered.</summary>
    [Fact]
    public void ConfiguredEngineController_NoCertificateFilesConfigured_Throws()
    {
        ConfiguredEngineController controller = new(new TestEngineController(), new NetworkConfig(), NoCurrentUser);

        Assert.Throws<InvalidOperationException>(() => controller.ConnectionOptions);
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

    private static TestFrame Alert(string body) => new() { Body = body, IsAlert = true };

    private sealed class CertificateFiles : IDisposable
    {
        public CertificateFiles() => Directory.CreateDirectory(StoreDirectory);

        public string StoreDirectory { get; } = Path.Combine(Path.GetTempPath(), $"comlink-cert-problem-{Guid.NewGuid():N}");
        public string Authority => Path.Combine(StoreDirectory, "authority.cer");
        public string Identity(string user) => Path.Combine(StoreDirectory, $"{user}.pfx");

        public void WriteAuthority(X509Certificate2Collection authorities) => File.WriteAllBytes(Authority, authorities[0].Export(X509ContentType.Cert));
        public void WriteIdentity(string user, X509Certificate2 certificate) => File.WriteAllBytes(Identity(user), certificate.Export(X509ContentType.Pfx));

        public void Dispose() => Directory.Delete(StoreDirectory, recursive: true);
    }

    /// <summary>A certificate file named for the user, issued to them and signed by the authority certificate, has no problem.</summary>
    [Fact]
    public void CertificateProblem_GoodCertificate_IsNull()
    {
        using CertificateFiles files = new();
        (Dictionary<string, X509Certificate2> identities, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("ALICE");
        files.WriteAuthority(authorities);
        files.WriteIdentity("ALICE", identities["ALICE"]);

        Assert.Null(MsmtCertificateLookup.GetProblem(files.Identity("ALICE"), files.Authority, "ALICE"));
    }

    /// <summary>Without the store or the authority, or without the user's file, there is a problem.</summary>
    [Fact]
    public void CertificateProblem_MissingPieces_AreReported()
    {
        using CertificateFiles files = new();
        (Dictionary<string, X509Certificate2> identities, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("ALICE");
        files.WriteAuthority(authorities);

        Assert.NotNull(MsmtCertificateLookup.GetProblem(null, files.Authority, "ALICE"));
        Assert.NotNull(MsmtCertificateLookup.GetProblem(files.Identity("ALICE"), null, "ALICE"));
        Assert.NotNull(MsmtCertificateLookup.GetProblem(files.Identity("ALICE"), files.Authority, "ALICE"));
        files.WriteIdentity("ALICE", identities["ALICE"]);
        Assert.NotNull(MsmtCertificateLookup.GetProblem(files.Identity("ALICE"), Path.Combine(files.StoreDirectory, "nope.cer"), "ALICE"));
    }

    /// <summary>A file named for one user that holds a certificate issued to someone else is a problem.</summary>
    [Fact]
    public void CertificateProblem_CertificateIssuedToSomeoneElse_IsReported()
    {
        using CertificateFiles files = new();
        (Dictionary<string, X509Certificate2> identities, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("ALICE", "BOB");
        files.WriteAuthority(authorities);
        files.WriteIdentity("ALICE", identities["BOB"]);

        Assert.Contains("not issued to ALICE", MsmtCertificateLookup.GetProblem(files.Identity("ALICE"), files.Authority, "ALICE"));
    }

    /// <summary>A certificate signed by an authority other than the authority certificate is a problem.</summary>
    [Fact]
    public void CertificateProblem_SignedByAnotherAuthority_IsReported()
    {
        using CertificateFiles files = new();
        (Dictionary<string, X509Certificate2> identities, _) = TestMsmtCertificates.CreateNamed("ALICE");
        (_, X509Certificate2Collection otherAuthorities) = TestMsmtCertificates.CreateNamed("BOB");
        files.WriteAuthority(otherAuthorities);
        files.WriteIdentity("ALICE", identities["ALICE"]);

        Assert.Contains("not signed by the authority certificate", MsmtCertificateLookup.GetProblem(files.Identity("ALICE"), files.Authority, "ALICE"));
    }

    /// <summary>The controller checks the files the network designates, and a network designating none has a problem for every user.</summary>
    [Fact]
    public void EngineController_GetCertificateProblem_UsesTheNetworksFiles()
    {
        using CertificateFiles files = new();
        (Dictionary<string, X509Certificate2> identities, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("ALICE");
        files.WriteAuthority(authorities);
        files.WriteIdentity("ALICE", identities["ALICE"]);

        EngineController designated = new(EngineBuilder.Build(new TestEngineConfiguration()), new CurrentUserProvider(), new NetworkConfig { CertificateStore = files.StoreDirectory, AuthorityCertificate = files.Authority });
        EngineController none = new(EngineBuilder.Build(new TestEngineConfiguration()), new CurrentUserProvider(), new NetworkConfig());

        Assert.Null(designated.GetCertificateProblem("ALICE"));
        Assert.NotNull(designated.GetCertificateProblem("BOB"));
        Assert.NotNull(none.GetCertificateProblem("ALICE"));
    }
}
