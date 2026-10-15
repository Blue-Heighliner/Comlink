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
        Assert.Throws<NotSupportedException>(() => controller.CreateFramePacket(new TestFrame(), 0, 1, 0, ReadOnlyMemory<byte>.Empty));
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
        Assert.IsType<TestPacket>(controller.CreateFramePacket(new TestFrame(), 0, 1, 0, ReadOnlyMemory<byte>.Empty));
    }

    /// <summary>Creating a packet through the controller and reading it back round-trips the fields of the host's packet type.</summary>
    [Fact]
    public void PacketEngineController_PacketFields_RoundTripThroughTheController()
    {
        IEngineController controller = new TestPacketEngineController();
        TestFrame frame = new();
        object packet = controller.CreateFramePacket(frame, 2, 5, 99, new byte[] { 1, 2, 3 });

        Assert.True(controller.IsFramePacket(packet));

        Assert.Equal(controller.GetFrameId(packet), controller.GetFrameId(controller.CreateFramePacket(frame, 3, 5, 99, new byte[] { 4 })));
        Assert.NotEqual(controller.GetFrameId(packet), controller.GetFrameId(controller.CreateFramePacket(new TestFrame(), 2, 5, 99, new byte[] { 1 })));
        Assert.Equal(2, controller.GetPacketIndex(packet));
        Assert.Equal(5, controller.GetPacketCount(packet));
        Assert.Equal(99, controller.GetFrameLength(packet));
        Assert.Equal(new byte[] { 1, 2, 3 }, controller.GetPacketPayload(packet).ToArray());
        Assert.Equal(new byte[] { 1, 2, 3 }, ((TestPacket)packet).Data);
    }

    /// <summary>Without packets the maximum payload size is 0 and the default window is 1, and a host can set both.</summary>
    [Fact]
    public void EngineController_MaxPayloadSizeAndWindow_HaveDefaultsAndAreOverridable()
    {
        TestEngineController defaults = new();
        TestPayloadSizeOverride overridden = new();

        Assert.Equal(0, defaults.MaxPayloadSize);
        Assert.Equal(1, defaults.PacketWindow);
        Assert.Equal(512, overridden.MaxPayloadSize);
        Assert.Equal(4, overridden.PacketWindow);
    }

    private sealed class TestPayloadSizeOverride : TestPacketEngineController
    {
        public override int MaxPayloadSize => 512;
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

    /// <summary>The data folder is whatever the wrapped provider says: the user is fixed by the certificate, so the decorator has nothing to add.</summary>
    [Fact]
    public void ConfiguredEngineController_AppDataPath_IsTheWrappedControllersOwn()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.AppDataPath).Returns("/base/path");

        Assert.Equal("/base/path", TestConfigured.Create(fallback.Object, new NetworkConfig(), NoCurrentUser).AppDataPath);
        Assert.Equal("/base/path", TestConfigured.Create(fallback.Object, new NetworkConfig { User = "alice" }, Me).AppDataPath);
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
        ConfiguredEngineController controller = TestConfigured.Create(fallback.Object, new NetworkConfig(), NoCurrentUser);

        Assert.Equal("FallbackApp", controller.AppName);
        Assert.Equal("9.8.7", controller.AppVersion);
        Assert.True(controller.IsKioskMode);
        Assert.Equal("FALLBACK-HOME", controller.HomeText);
    }

    /// <summary>User info is left entirely to the wrapped provider, since there is no corresponding network file field.</summary>
    [Fact]
    public void ConfiguredEngineController_UserInfo_AlwaysDelegatesToFallback()
    {
        UserInfo fallbackInfo = new() { Name = "X" };
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.GetUserInfo("X")).Returns(fallbackInfo);
        ConfiguredEngineController controller = TestConfigured.Create(fallback.Object, new NetworkConfig(), NoCurrentUser);

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
        Assert.True(controller.IsDraftAllowed(Mock.Of<IEngineContext>(), TestMessagePriority.Normal, null, null, "ANY"));
    }

    /// <summary>Priorities returns the same list instance/values on every access.</summary>
    [Fact]
    public void EngineController_Priorities_IsStableAcrossCalls()
    {
        TestEngineController controller = new();
        Assert.Equal(controller.Priorities, controller.Priorities);
    }

    /// <summary>Falls back to the wrapped provider for TagsEnabled/TagLabel when not configured; Priorities/IsDraftAllowed always delegate.</summary>
    [Fact]
    public void ConfiguredEngineController_FallsBackWhenMessageCompositionNotConfigured()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.TagsEnabled).Returns(false);
        fallback.Setup(f => f.TagLabel).Returns("Category");
        IReadOnlyList<MessagePriorityOption> priorities = [new MessagePriorityOption { Name = "Low", Value = 0, Key = TestMessagePriority.Low }];
        fallback.Setup(f => f.Priorities).Returns(priorities);
        fallback.Setup(f => f.IsDraftAllowed(It.IsAny<IEngineContext>(), TestMessagePriority.Low, null, null, "SPAM")).Returns(false);
        ConfiguredEngineController controller = TestConfigured.Create(fallback.Object, new NetworkConfig(), NoCurrentUser);

        Assert.False(controller.TagsEnabled);
        Assert.Equal("Category", controller.TagLabel);
        Assert.Same(priorities, controller.Priorities);
        Assert.False(controller.IsDraftAllowed(Mock.Of<IEngineContext>(), TestMessagePriority.Low, null, null, "SPAM"));
    }

    /// <summary>Falls back to the wrapped provider when not configured.</summary>
    [Fact]
    public void ConfiguredEngineController_FallsBackWhenPrintPolicyNotConfigured()
    {
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.PrintReceivedDefaultEnabled).Returns(true);
        ConfiguredEngineController controller = TestConfigured.Create(fallback.Object, new NetworkConfig(), NoCurrentUser);

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
        ConfiguredEngineController controller = TestConfigured.Create(fallback.Object, new NetworkConfig(), NoCurrentUser);

        Assert.False(controller.CanDelete(FolderType.Drafts));
    }

    /// <summary>ConnectionOptions throws when no current user is installed, since MSMT peer authentication is mandatory and there is no user to resolve an identity certificate for.</summary>
    [Fact]
    public void EngineController_ConnectionOptions_NoCurrentUser_Throws()
    {
        TestEngineController controller = new();

        Assert.Throws<InvalidOperationException>(() => controller.ConnectionOptions);
    }

    /// <summary>ConnectionOptions loads the identity from the node's user certificate file and the trusted authority from its authority file.</summary>
    [Fact]
    public void ConfiguredEngineController_ConnectionOptions_LoadsFromTheIdentityFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"comlink-cert-file-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            (X509Certificate2 server, X509Certificate2 client, X509Certificate2Collection trustedAuthorities) = TestMsmtCertificates.Create();
            string authorityFile = Path.Combine(tempDir, "authority.cer");
            string certificateFile = Path.Combine(tempDir, "certificate.pfx");
            File.WriteAllBytes(certificateFile, client.Export(X509ContentType.Pfx));
            File.WriteAllBytes(authorityFile, trustedAuthorities[0].Export(X509ContentType.Cert));

            ConfiguredEngineController controller = TestConfigured.Create(new TestEngineController(), new NetworkConfig(), Me, TestConfigured.Identity(authorityFile, certificateFile, "ME"));

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

    /// <summary>A certificate file that is missing from where the identity says it is makes ConnectionOptions throw.</summary>
    [Fact]
    public void ConfiguredEngineController_IdentityFileMissing_Throws()
        => Assert.Throws<InvalidOperationException>(() => TestConfigured.Create(new TestEngineController(), new NetworkConfig(), Me, TestConfigured.Identity("/nonexistent/authority.cer", "/nonexistent/certificate.pfx", "ME")).ConnectionOptions);

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
        ConfiguredEngineController controller = TestConfigured.Create(fallback.Object, new NetworkConfig(), NoCurrentUser);

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

    /// <summary>By default no handshake is configured.</summary>
    [Fact]
    public void EngineController_NoHandshake()
    {
        TestEngineController controller = new();

        Assert.Null(controller.PacketHandshakeHandler);
        Assert.Null(controller.FrameHandshakeHandler);
    }

    /// <summary>Every handler's connection description is told which user this node runs as, so what they send can say who is speaking.</summary>
    [Fact]
    public void ConfiguredEngineController_ConnectionDescriptions_SeeTheLocalUser()
    {
        IpConnectionInfo connection = new() { Host = "10.0.0.1" };
        Mock<IEngineController> fallback = new();
        ConfiguredEngineController controller = TestConfigured.Create(fallback.Object, new NetworkConfig(), Me);

        IpConnectionInfo expected = connection with { LocalUser = "ME" };
        Assert.Equal("ME", controller.WithLocalUser(connection).LocalUser);
        Assert.Equal(expected, controller.WithLocalUser(connection));
    }

    /// <summary>The handshake and frame handlers are not configurable from the network file and come from the wrapped provider.</summary>
    [Fact]
    public void ConfiguredEngineController_Handlers_DelegateToFallback()
    {
        IHandshakeHandler packets = Mock.Of<IHandshakeHandler>();
        IHandshakeHandler frames = Mock.Of<IHandshakeHandler>();
        IEngineFrameHandler network = Mock.Of<IEngineFrameHandler>();
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.PacketHandshakeHandler).Returns(packets);
        fallback.Setup(f => f.FrameHandshakeHandler).Returns(frames);
        fallback.Setup(f => f.FrameHandler).Returns(network);
        ConfiguredEngineController controller = TestConfigured.Create(fallback.Object, new NetworkConfig(), NoCurrentUser);

        Assert.Same(packets, controller.PacketHandshakeHandler);
        Assert.Same(frames, controller.FrameHandshakeHandler);
        Assert.Same(network, controller.FrameHandler);
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
        ConfiguredEngineController controller = TestConfigured.Create(fallback.Object, new NetworkConfig(), NoCurrentUser);

        Assert.False(controller.CommandLineOverridesAllowed);
    }

    /// <summary>The packet type, serializer and field members have no network file field and delegate straight to the wrapped controller.</summary>
    [Fact]
    public void ConfiguredEngineController_PacketMembers_DelegateToFallback()
    {
        TestPacketEngineController fallback = new();
        ConfiguredEngineController controller = TestConfigured.Create(fallback, new NetworkConfig(), NoCurrentUser);

        Assert.Equal(typeof(TestPacket), controller.PacketType);
        Assert.Same(fallback.PacketSerializer, controller.PacketSerializer);
        Assert.Equal(fallback.MaxPayloadSize, controller.MaxPayloadSize);
        Assert.Equal(fallback.PacketWindow, controller.PacketWindow);
        object packet = controller.CreateFramePacket(new TestFrame(), 1, 2, 30, new byte[] { 5 });
        Assert.NotEmpty(controller.GetFrameId(packet));
        Assert.Equal(1, controller.GetPacketIndex(packet));
        Assert.Equal(2, controller.GetPacketCount(packet));
        Assert.Equal(30, controller.GetFrameLength(packet));
        Assert.Equal(new byte[] { 5 }, controller.GetPacketPayload(packet).ToArray());
    }

    /// <summary>ExternalSystems has no network file field and always delegates to the wrapped provider.</summary>
    [Fact]
    public void ConfiguredEngineController_ExternalSystems_AlwaysDelegatesToFallback()
    {
        Mock<IExternalSystem> externalSystem = new();
        Mock<IEngineController> fallback = new();
        fallback.Setup(f => f.ExternalSystems).Returns([externalSystem.Object]);
        ConfiguredEngineController controller = TestConfigured.Create(fallback.Object, new NetworkConfig(), NoCurrentUser);

        Assert.Same(externalSystem.Object, Assert.Single(controller.ExternalSystems));
    }

    /// <summary>With neither certificate file field configured, ConnectionOptions falls back to the system store lookup - and throws the same way DefaultEngineController does when no current user is registered.</summary>
    [Fact]
    public void ConfiguredEngineController_NoCertificateFilesConfigured_Throws()
    {
        ConfiguredEngineController controller = TestConfigured.Create(new TestEngineController(), new NetworkConfig(), NoCurrentUser);

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

    /// <summary>The identity loaded from a file keeps an exportable private key, which MSMT needs to complete a TLS handshake on Windows.</summary>
    [Fact]
    public void MsmtCertificateLookup_BuildPeerOptionsFromFiles_IdentityKeyIsExportable()
    {
        using CertificateFiles files = new();
        (Dictionary<string, X509Certificate2> identities, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("ALICE");
        files.WriteAuthority(authorities);
        files.WriteIdentity("ALICE", identities["ALICE"]);

        MsmtSessionPeerOptions options = MsmtCertificateLookup.BuildPeerOptionsFromFiles(files.Identity("ALICE"), files.Authority);

        using RSA key = options.Credentials!.Identity.GetRSAPrivateKey()!;
        key.ExportParameters(includePrivateParameters: true);
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

    private static EngineBuilder BuilderFor(CertificateFiles files)
    {
        EngineBuilder builder = EngineBuilder.Build(new TestEngineConfiguration());
        builder.AuthorityCertificateValue = files.Authority;
        builder.UserCertificateValue = files.Identity("Certificate");
        return builder;
    }

    private static NetworkConfig NetworkOf(CertificateFiles files, string[] args, params string[] users)
    {
        NetworkConfig network = NetworkConfig.Load(args, files.StoreDirectory);
        network.Users = users.ToDictionary(name => name, _ => new NetworkUserConfig(), StringComparer.OrdinalIgnoreCase);
        return network;
    }

    /// <summary>A user certificate that chains to the authority and is issued to a user of the network names the node: the user is spelled as the network file spells it, and the files and thumbprints are recorded.</summary>
    [Fact]
    public void ResolveIdentity_GoodCertificate_NamesTheUserAsTheNetworkSpellsIt()
    {
        using CertificateFiles files = new();
        (Dictionary<string, X509Certificate2> identities, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("Alice");
        files.WriteAuthority(authorities);
        files.WriteIdentity("Certificate", identities["Alice"]);

        NodeIdentity identity = MsmtCertificateLookup.ResolveIdentity(BuilderFor(files), NetworkOf(files, [], "ALICE", "BOB"));

        Assert.Equal(("ALICE", files.Authority, files.Identity("Certificate")), (identity.UserName, identity.AuthorityFile, identity.CertificateFile));
        Assert.Equal((identities["Alice"].Thumbprint, authorities[0].Thumbprint), (identity.CertificateThumbprint, identity.AuthorityThumbprint));
    }

    /// <summary>The --authority and --certificate arguments name the files in place of the ones the host stated.</summary>
    [Fact]
    public void ResolveIdentity_CommandLineFiles_ReplaceTheStatedOnes()
    {
        using CertificateFiles files = new();
        (Dictionary<string, X509Certificate2> identities, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("ALICE");
        files.WriteAuthority(authorities);
        files.WriteIdentity("Other", identities["ALICE"]);
        EngineBuilder builder = EngineBuilder.Build(new TestEngineConfiguration());

        NodeIdentity identity = MsmtCertificateLookup.ResolveIdentity(builder, NetworkOf(files, ["--authority", files.Authority, "--certificate", files.Identity("Other")], "ALICE"));

        Assert.Equal(("ALICE", files.Identity("Other")), (identity.UserName, identity.CertificateFile));
    }

    /// <summary>A certificate or authority that is neither stated nor given on the command line, or whose file is missing, stops the node from starting.</summary>
    [Fact]
    public void ResolveIdentity_MissingPieces_Throw()
    {
        using CertificateFiles files = new();
        (Dictionary<string, X509Certificate2> identities, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("ALICE");
        files.WriteAuthority(authorities);
        EngineBuilder none = EngineBuilder.Build(new TestEngineConfiguration());
        NetworkConfig network = NetworkOf(files, [], "ALICE");

        Assert.Contains("no authority certificate", Assert.Throws<InvalidOperationException>(() => MsmtCertificateLookup.ResolveIdentity(none, network)).Message);
        none.AuthorityCertificateValue = files.Authority;
        Assert.Contains("no user certificate", Assert.Throws<InvalidOperationException>(() => MsmtCertificateLookup.ResolveIdentity(none, network)).Message);
        Assert.Contains("was not found", Assert.Throws<InvalidOperationException>(() => MsmtCertificateLookup.ResolveIdentity(BuilderFor(files), network)).Message);
        files.WriteIdentity("Certificate", identities["ALICE"]);
        File.Delete(files.Authority);
        Assert.Contains("authority certificate file", Assert.Throws<InvalidOperationException>(() => MsmtCertificateLookup.ResolveIdentity(BuilderFor(files), network)).Message);
    }

    /// <summary>A certificate issued to someone the network does not list, or to someone other than the --user argument names, is refused.</summary>
    [Fact]
    public void ResolveIdentity_CertificateNotIssuedToAListedOrNamedUser_Throws()
    {
        using CertificateFiles files = new();
        (Dictionary<string, X509Certificate2> identities, X509Certificate2Collection authorities) = TestMsmtCertificates.CreateNamed("ALICE");
        files.WriteAuthority(authorities);
        files.WriteIdentity("Certificate", identities["ALICE"]);

        Assert.Contains("not a user of the network", Assert.Throws<InvalidOperationException>(() => MsmtCertificateLookup.ResolveIdentity(BuilderFor(files), NetworkOf(files, [], "BOB"))).Message);
        Assert.Contains("not issued to BOB", Assert.Throws<InvalidOperationException>(() => MsmtCertificateLookup.ResolveIdentity(BuilderFor(files), NetworkOf(files, ["--user", "BOB"], "ALICE", "BOB"))).Message);
        Assert.Equal("ALICE", MsmtCertificateLookup.ResolveIdentity(BuilderFor(files), NetworkOf(files, ["--user", "alice"], "ALICE", "BOB")).UserName);
    }

    /// <summary>A certificate signed by an authority other than the authority certificate is refused.</summary>
    [Fact]
    public void ResolveIdentity_SignedByAnotherAuthority_Throws()
    {
        using CertificateFiles files = new();
        (Dictionary<string, X509Certificate2> identities, _) = TestMsmtCertificates.CreateNamed("ALICE");
        (_, X509Certificate2Collection otherAuthorities) = TestMsmtCertificates.CreateNamed("BOB");
        files.WriteAuthority(otherAuthorities);
        files.WriteIdentity("Certificate", identities["ALICE"]);

        Assert.Contains("not signed by the authority certificate", Assert.Throws<InvalidOperationException>(() => MsmtCertificateLookup.ResolveIdentity(BuilderFor(files), NetworkOf(files, [], "ALICE"))).Message);
    }
}
