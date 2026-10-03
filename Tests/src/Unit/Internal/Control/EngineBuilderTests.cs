namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Control;

/// <summary>Unit tests for <see cref="EngineBuilder"/>: what a host states in its configuration is what <see cref="EngineController"/> reports, and anything left unstated takes the default.</summary>
public sealed class EngineBuilderTests
{
    private sealed class Configuration(Func<IEngineBuilder, IEngineBuilder> configure) : IEngineConfiguration
    {
        public IEngineBuilder Configure(IEngineBuilder engine) => configure(new TestEngineConfiguration().Configure(engine));
    }

    private sealed class PacketConfiguration(Func<IEngineBuilder, IEngineBuilder> configure) : IEngineConfiguration
    {
        public IEngineBuilder Configure(IEngineBuilder engine) => configure(new TestEngineConfiguration(packets: true).Configure(engine));
    }

    private static IServiceProvider Services(params object[] processors)
    {
        ServiceCollection services = new();
        foreach (object processor in processors)
        {
            foreach (Type type in processor.GetType().GetInterfaces().Where(type => type.IsGenericType && type.Namespace == typeof(IInitialFrameProcessor<>).Namespace && type.Name.EndsWith("Processor`1"))) { services.AddSingleton(type, processor); }
        }

        return services.BuildServiceProvider();
    }

    private static (EngineBuilder Builder, EngineController Controller) BuildWith(Action<IFrameBuilder<TestFrame>>? message = null, Action<IPacketBuilder<TestPacket>>? packet = null, IServiceProvider? services = null)
    {
        EngineBuilder builder = packet is null ? EngineBuilder.Build(new TestEngineConfiguration(false, message)) : EngineBuilder.Build(new TestEngineConfiguration(false, message, packet));
        return (builder, new EngineController(builder, new CurrentUserProvider(), null, services));
    }

    private static (EngineBuilder Builder, EngineController Controller) Build(Func<IEngineBuilder, IEngineBuilder> configure, string? currentUser = null, NetworkConfig? network = null)
    {
        EngineBuilder builder = EngineBuilder.Build(new Configuration(configure));
        return (builder, new EngineController(builder, new CurrentUserProvider { UserName = currentUser }, network));
    }

    private static NetworkConfig Network(params (string Name, NetworkUserConfig User)[] users)
        => new() { Users = users.ToDictionary(user => user.Name, user => user.User) };

    /// <summary>A configuration that never states a frame type cannot start the engine.</summary>
    [Fact]
    public void Build_WithoutMessageType_Throws()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => EngineBuilder.Build(new EmptyConfiguration()));

        Assert.Contains("Frames<TFrame>", error.Message);
    }

    private sealed class EmptyConfiguration : IEngineConfiguration
    {
        public IEngineBuilder Configure(IEngineBuilder engine) => engine;
    }

    /// <summary>The engine cannot build a controller from a builder that has no frame mapping.</summary>
    [Fact]
    public void Controller_WithoutMessageMapping_Throws()
        => Assert.Throws<InvalidOperationException>(() => new EngineController(new EngineBuilder(), new CurrentUserProvider()));

    /// <summary>Configure is run exactly once, against the builder the engine will read.</summary>
    [Fact]
    public void Build_RunsConfigurationOnceAndKeepsWhatItStated()
    {
        int calls = 0;
        EngineBuilder builder = EngineBuilder.Build(new Configuration(engine => { calls++; return engine.AppName("MyApp"); }));

        Assert.Equal(1, calls);
        Assert.Equal("MyApp", builder.AppNameValue);
    }

    /// <summary>Settings left unstated take the engine's defaults.</summary>
    [Fact]
    public void Defaults_AreTheEnginesOwn()
    {
        (_, EngineController controller) = Build(engine => engine);

        Assert.Equal("HOME", controller.HomeText);
        Assert.False(controller.IsKioskMode);
        Assert.Null(controller.WindowIconPath);
        Assert.Null(controller.DebugUserName);
        Assert.Equal(50021, controller.PeerPort);
        Assert.Equal(50020, controller.InterfacePort);
        Assert.Equal("ALERT", controller.AlertLabel);
        Assert.Equal(TimeSpan.FromSeconds(30), controller.AlarmSoundDuration);
        Assert.True(controller.QuickConfirmationEnabled);
        Assert.True(controller.ComposeAlertsEnabled);
        Assert.Equal("Tag", controller.TagLabel);
        Assert.True(controller.TagsEnabled);
        Assert.False(controller.PrintReceivedDefaultEnabled);
        Assert.Equal(UserRole.Peer, controller.Role);
        Assert.Equal("COMLINK-ROOT", controller.TrustedAuthorityCertificateName);
        Assert.False(controller.CommandLineOverridesAllowed);
        Assert.Empty(controller.OutgoingPoints);
        Assert.Empty(controller.Servers);
        Assert.Empty(controller.ExternalSystems);
        Assert.Null(controller.ExternalServer);
        Assert.Null(controller.NetworkHandler);
        Assert.Null(controller.PacketType);
        Assert.Single(controller.Priorities);
        Assert.Equal("Normal", controller.Priorities[0].Name);
        Assert.Equal("USER", controller.GetCertificateName("USER"));
        Assert.True(controller.CanDelete(FolderType.Inbox));
        Assert.Equal(1, controller.GetPrintCount(new TestFrame()));
        Assert.Equal([AddressType.To, AddressType.Cc, AddressType.External], controller.AddressTypes.Select(t => t.Type));
        Assert.Equal(["To", "Cc", "External"], controller.AddressTypes.Select(t => t.Label));
    }

    /// <summary>Everything a host can state about the application and the ports is reported back.</summary>
    [Fact]
    public void Stated_AppAndPortSettings_AreReported()
    {
        const string icon = "avares://Host/icon.png";
        (_, EngineController controller) = Build(engine => engine
            .AppName("MyApp").AppVersion("2.3.4").KioskMode().HomeText("Welcome").WindowIcon(icon)
            .DebugUser("DEBUG").CommandLineOverrides(true));

        Assert.Equal("MyApp", controller.AppName);
        Assert.Equal("2.3.4", controller.AppVersion);
        Assert.True(controller.IsKioskMode);
        Assert.Equal("Welcome", controller.HomeText);
        Assert.Equal(icon, controller.WindowIconPath);
        Assert.Equal("DEBUG", controller.DebugUserName);
        Assert.True(controller.CommandLineOverridesAllowed);
    }

    /// <summary>The data directory is the current user's own folder inside the application's folder under the application data root, and the application's folder itself before a user exists, where the install state always lives.</summary>
    [Fact]
    public void AppDataPath_IsTheCurrentUsersFolder()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        (_, EngineController noUser) = Build(engine => engine.AppName("MyApp"));
        (_, EngineController alice) = Build(engine => engine.AppName("MyApp"), "ALICE");

        Assert.Equal(root, alice.AppDataRoot);
        Assert.Equal(Path.Combine(root, "MyApp"), noUser.AppDataPath);
        Assert.Equal(Path.Combine(root, "MyApp", "ALICE"), alice.AppDataPath);
        Assert.Equal(Path.Combine(root, "MyApp", "State.json"), alice.StatePath);
    }

    /// <summary>Alert, tag, priority and print settings a host states replace the defaults.</summary>
    [Fact]
    public void Stated_CompositionAndAlertSettings_AreReported()
    {
        (_, EngineController controller) = Build(engine => engine
            .AlertLabel("ALARM").AlarmDuration(TimeSpan.FromSeconds(5)).QuickConfirmation(false).ComposeAlerts(false)
            .Priorities(("Low", 0), ("High", 9))
            .Tags(false, "Category").BlockTag("SPAM", null).BlockTag(null, 9)
            .PrintReceived()
            .CanDelete(folder => folder == FolderType.Drafts));

        Assert.Equal("ALARM", controller.AlertLabel);
        Assert.Equal(TimeSpan.FromSeconds(5), controller.AlarmSoundDuration);
        Assert.False(controller.QuickConfirmationEnabled);
        Assert.False(controller.ComposeAlertsEnabled);
        Assert.Equal(["Low", "High"], controller.Priorities.Select(p => p.Name));
        Assert.False(controller.TagsEnabled);
        Assert.Equal("Category", controller.TagLabel);
        Assert.Equal(2, controller.BlockedCombinations.Count);
        Assert.True(controller.PrintReceivedDefaultEnabled);
        Assert.True(controller.CanDelete(FolderType.Drafts));
        Assert.False(controller.CanDelete(FolderType.Inbox));
    }

    /// <summary>Turning tags on or off without a label keeps the default label.</summary>
    [Fact]
    public void Tags_WithoutLabel_KeepsTheDefaultLabel()
    {
        (_, EngineController controller) = Build(engine => engine.Tags(false));

        Assert.False(controller.TagsEnabled);
        Assert.Equal("Tag", controller.TagLabel);
    }

    /// <summary>An overridden address type label replaces the default for that type only; the others keep theirs.</summary>
    [Fact]
    public void AddressTypeLabel_Stated_ReplacesOnlyThatTypesDefault()
    {
        (_, EngineController controller) = Build(engine => engine.AddressTypeLabel(AddressType.External, "OUTSIDE"));

        Assert.Equal(["To", "Cc", "OUTSIDE"], controller.AddressTypes.Select(t => t.Label));
        Assert.Equal([AddressType.To, AddressType.Cc, AddressType.External], controller.AddressTypes.Select(t => t.Type));
    }

    /// <summary>Stating priorities twice replaces the earlier list rather than adding to it.</summary>
    [Fact]
    public void Priorities_StatedTwice_ReplacesTheEarlierList()
    {
        (_, EngineController controller) = Build(engine => engine
            .Priorities(("A", 1))
            .Priorities(("B", 2)));

        Assert.Equal("B", Assert.Single(controller.Priorities).Name);
    }

    /// <summary>Users and groups from the host and the network file are merged, with the file winning for a group of the same name, and the data attached to a user comes from their entry.</summary>
    [Fact]
    public void Stated_UsersGroupsAndData_AreReported()
    {
        (_, EngineController controller) = Build(engine => engine
            .Users("ALICE", "BOB").Users("CAROL")
            .Group("OPS", "ALICE", "BOB").Group("ALL", "OPS", "CAROL"),
            network: new NetworkConfig
            {
                Users = { ["alice"] = new NetworkUserConfig { Data = new Dictionary<string, string> { ["desk"] = "4" } }, ["DAVE"] = new NetworkUserConfig() },
                UserGroups = { ["OPS"] = ["ALICE", "BOB", "DAVE"], ["EXTRA"] = ["DAVE"] }
            });

        Assert.Equal(["ALICE", "BOB", "CAROL", "DAVE", "OPS", "ALL", "EXTRA"], controller.Users);
        Assert.Equal(["ALICE", "BOB", "DAVE"], controller.UserGroups["ops"]);
        Assert.Equal(["OPS", "CAROL"], controller.UserGroups["ALL"]);
        Assert.Equal(["DAVE"], controller.UserGroups["extra"]);
        Assert.Equal("4", controller.GetUserData("ALICE")["desk"]);
        Assert.Empty(controller.GetUserData("BOB"));
        Assert.Equal(["OPS", "EXTRA"], controller.GetUserInfo("DAVE").Groups);
    }

    /// <summary>A user with nothing stated has no data.</summary>
    [Fact]
    public void GetUserData_NothingStated_IsEmpty()
    {
        (_, EngineController controller) = Build(engine => engine);

        Assert.Empty(controller.GetUserData("ANYONE"));
    }

    /// <summary>Installation codes resolve to a user name through the stated resolver; by default a code is the name of a user of the network, and otherwise only the code CODE is recognized.</summary>
    [Fact]
    public void UserCodes_StatedResolverReplacesTheDefault()
    {
        (_, EngineController stated) = Build(engine => engine.UserCodes(code => code == "X" ? "XUSER" : null));
        (_, EngineController fallback) = Build(engine => engine);

        Assert.Equal("XUSER", stated.ResolveUserName("X"));
        Assert.Null(stated.ResolveUserName("CODE"));
        Assert.Equal("TEST", fallback.ResolveUserName("code"));
        Assert.Null(fallback.ResolveUserName("X"));

        (_, EngineController networked) = Build(engine => engine.Users("ALICE"), network: Network(("BOB", new NetworkUserConfig())));
        Assert.Equal("ALICE", networked.ResolveUserName("alice"));
        Assert.Equal("BOB", networked.ResolveUserName("Bob"));
        Assert.Equal("TEST", networked.ResolveUserName("CODE"));
        Assert.Null(networked.ResolveUserName("NOBODY"));
    }

    /// <summary>A user the network does not list is just a name; a listed user's details are what the file says.</summary>
    [Fact]
    public void GetUserInfo_ReturnsTheNetworksDetailsOrJustTheName()
    {
        (_, EngineController controller) = Build(engine => engine, network: Network(("ALICE", new NetworkUserConfig { SecurityLevel = "HIGH" })));

        Assert.Equal("HIGH", controller.GetUserInfo("alice").SecurityLevel);
        UserInfo other = controller.GetUserInfo("BOB");
        Assert.Equal("BOB", other.Name);
        Assert.Null(other.Role);
    }

    /// <summary>The current user's role, ports, links and connection points come from that user's entry, with defaults before a user exists or when none is listed.</summary>
    [Fact]
    public void CurrentUserInfo_DecidesRolePortsAndConnections()
    {
        NetworkConfig network = Network(("SERVER", new NetworkUserConfig
        {
            Role = "Server",
            PeerPoint = new PeerPointConfig { Host = "10.0.0.9", Port = 1234 },
            InterfacePort = 5678,
            Parent = "UPSTREAM",
            Children = ["C1", new NetworkLinkConfig { User = "C2", Mode = "MsmtConnect" }, new NetworkLinkConfig { User = "C3", Mode = "SyncSerial", SerialPort = "SL0", SerialAddress = 1, RemoteSerialAddress = 2 }]
        }),
        ("UPSTREAM", new NetworkUserConfig { Role = "Server", PeerPoint = new PeerPointConfig { Host = "10.0.0.1", Port = 1 } }),
        ("C2", new NetworkUserConfig { Role = "Client", PeerPoint = new PeerPointConfig { Host = "10.0.0.2", Port = 2 } }));
        (_, EngineController server) = Build(engine => engine, "SERVER", network);
        (_, EngineController noInfo) = Build(engine => engine, "OTHER", network);
        (_, EngineController noUser) = Build(engine => engine, network: network);

        Assert.Equal((UserRole.Server, 1234, 5678), (server.Role, server.PeerPort, server.InterfacePort));
        Assert.Equal(
            [new ConnectionPoint { IpAddress = "10.0.0.1", Port = 1 }, new ConnectionPoint { IpAddress = "10.0.0.2", Port = 2 }, new ConnectionPoint { SerialPort = "SL0", SerialAddress = 1, RemoteSerialAddress = 2, User = "C3" }],
            server.OutgoingPoints);
        Assert.Equal(("UPSTREAM", new ConnectionPoint { IpAddress = "10.0.0.1", Port = 1 }), (server.ParentUser, server.ParentPoint));
        Assert.Equal(["C1", "C2", "C3"], server.Servers["server"].Children);
        Assert.All([noInfo, noUser], controller =>
        {
            Assert.Equal((UserRole.Peer, 50021, 50020), (controller.Role, controller.PeerPort, controller.InterfacePort));
            Assert.Empty(controller.OutgoingPoints);
            Assert.Equal(2, controller.Servers.Count);
        });
    }

    /// <summary>A peer dials every other peer that states a peer point, but when both state one only the lower name dials, so a pair never connects both ways; a peer with no peer point dials all of them.</summary>
    [Fact]
    public void Peers_DialEachOthersPeerPoints_OnlyOneWay()
    {
        NetworkConfig network = Network(
            ("ALICE", new NetworkUserConfig { PeerPoint = new PeerPointConfig { Host = "10.0.0.1", Port = 1 } }),
            ("BOB", new NetworkUserConfig { PeerPoint = new PeerPointConfig { Host = "10.0.0.2", Port = 2 } }),
            ("CAROL", new NetworkUserConfig()),
            ("SERVER", new NetworkUserConfig { Role = "Server", PeerPoint = new PeerPointConfig { Host = "10.0.0.9", Port = 9 } }));
        (_, EngineController alice) = Build(engine => engine, "ALICE", network);
        (_, EngineController bob) = Build(engine => engine, "BOB", network);
        (_, EngineController carol) = Build(engine => engine, "CAROL", network);

        Assert.Equal([new ConnectionPoint { IpAddress = "10.0.0.2", Port = 2 }], alice.OutgoingPoints);
        Assert.Empty(bob.OutgoingPoints);
        Assert.Equal([new ConnectionPoint { IpAddress = "10.0.0.1", Port = 1 }, new ConnectionPoint { IpAddress = "10.0.0.2", Port = 2 }], carol.OutgoingPoints);
    }

    /// <summary>A parent is dialed by default and a child listened for; a forced mode reverses that, and listening for a parent leaves no point to dial.</summary>
    [Fact]
    public void Links_DefaultToConnectingToParentsAndListeningForChildren_UnlessForced()
    {
        NetworkConfig network = Network(
            ("PARENT", new NetworkUserConfig { Role = "Server", PeerPoint = new PeerPointConfig { Host = "10.0.0.1", Port = 1 } }),
            ("CHILD", new NetworkUserConfig { Role = "Client", PeerPoint = new PeerPointConfig { Host = "10.0.0.2", Port = 2 }, Parent = "PARENT" }),
            ("LISTENER", new NetworkUserConfig { Role = "Client", Parent = new NetworkLinkConfig { User = "PARENT", Mode = "MsmtListen" } }));
        (_, EngineController child) = Build(engine => engine, "CHILD", network);
        (_, EngineController listener) = Build(engine => engine, "LISTENER", network);

        Assert.Equal(new ConnectionPoint { IpAddress = "10.0.0.1", Port = 1 }, child.ParentPoint);
        Assert.Equal("PARENT", listener.ParentUser);
        Assert.Null(listener.ParentPoint);
        Assert.Empty(listener.OutgoingPoints);
    }

    /// <summary>A server's topology records which of its children are relays and the clients behind each, taken from the relay's own entry.</summary>
    [Fact]
    public void Servers_RecordTheClientsBehindEachRelay()
    {
        NetworkConfig network = Network(
            ("SERVER", new NetworkUserConfig { Role = "Server", Children = ["C1", "RELAY"] }),
            ("RELAY", new NetworkUserConfig { Role = "Relay", Children = ["C2", "C3"] }));
        (_, EngineController controller) = Build(engine => engine, "SERVER", network);

        ServerUserConfig server = controller.Servers["SERVER"];

        Assert.Equal(["C1", "RELAY"], server.Children);
        Assert.Equal(["C2", "C3"], Assert.Single(server.Relays).Value);
        Assert.Equal("RELAY", Assert.Single(server.Relays).Key);
        Assert.DoesNotContain("RELAY", controller.Servers.Keys);
    }

    /// <summary>Certificate settings a host states are used, including for the MSMT options when it supplies its own.</summary>
    [Fact]
    public void Stated_CertificateSettings_AreUsed()
    {
        MsmtSessionPeerOptions options = new() { Credentials = new MsmtCredentials { Identity = TestMsmtCertificates.Create().Server, TrustedAuthorities = [] } };
        (_, EngineController controller) = Build(
            engine => engine.TrustedAuthority("MY-ROOT").ConnectionOptions(() => options),
            network: Network(("BOB", new NetworkUserConfig { CertificateName = "cert-BOB" })));
        (_, EngineController fromFile) = Build(engine => engine.TrustedAuthority("MY-ROOT"), network: new NetworkConfig { TrustedAuthorityCertificateName = "FILE-ROOT" });

        Assert.Equal("cert-BOB", controller.GetCertificateName("BOB"));
        Assert.Equal("CAROL", controller.GetCertificateName("CAROL"));
        Assert.Equal("MY-ROOT", controller.TrustedAuthorityCertificateName);
        Assert.Equal("FILE-ROOT", fromFile.TrustedAuthorityCertificateName);
        Assert.Same(options, controller.ConnectionOptions);
    }

    /// <summary>The stated MSMT options are used with the engine's own credentials, and the built options are used as they are when none are stated.</summary>
    [Fact]
    public void Stated_MsmtOptions_ReplaceTheSettingsUsedForEveryConnectionKeepingTheEnginesCredentials()
    {
        MsmtSessionPeerOptions options = new() { Credentials = new MsmtCredentials { Identity = TestMsmtCertificates.Create().Server, TrustedAuthorities = [] } };
        (_, EngineController adjusted) = Build(engine => engine
            .ConnectionOptions(() => options).MsmtOptions(new MsmtConnectionOptions { HandshakeTimeout = TimeSpan.FromSeconds(7) }));
        (_, EngineController plain) = Build(engine => engine.ConnectionOptions(() => options));

        Assert.Equal(TimeSpan.FromSeconds(7), adjusted.ConnectionOptions.HandshakeTimeout);
        Assert.Same(options.Credentials, adjusted.ConnectionOptions.Credentials);
        Assert.Equal(options.HandshakeTimeout, plain.ConnectionOptions.HandshakeTimeout);
    }

    /// <summary>The stated MicroGate options are used, and the defaults when none are stated.</summary>
    [Fact]
    public void Stated_MicroGateOptions_ReplaceTheDefaults()
    {
        (_, EngineController adjusted) = Build(engine => engine
            .MicroGateOptions(new MicroGatePeerOptions { MaxInfoField = 512, Link = new MicroGatePeerOptions().Link with { Crc = MicroGateCrc.Crc32Ccitt } }));
        (_, EngineController plain) = Build(engine => engine);

        Assert.Equal(512, adjusted.MicroGateOptions.MaxInfoField);
        Assert.Equal(MicroGateCrc.Crc32Ccitt, adjusted.MicroGateOptions.Link.Crc);
        Assert.Equal(new MicroGatePeerOptions(), plain.MicroGateOptions);
    }

    /// <summary>The print count function stated with the message is used for received messages.</summary>
    [Fact]
    public void Stated_PrintCount_IsUsed()
    {
        (_, EngineController controller) = BuildWith(message => message.PrintCount(m => m.IsAlert ? 2 : 1));

        Assert.Equal(2, controller.GetPrintCount(new TestFrame { IsAlert = true }));
        Assert.Equal(1, controller.GetPrintCount(new TestFrame()));
    }

    /// <summary>The identification hook and the initial message and packet processors are reported and used.</summary>
    [Fact]
    public void Stated_Identification_IsUsed()
    {
        IpConnectionInfo info = new() { Host = "10.0.0.1" };
        Mock<IInitialFrameProcessor<TestFrame>> messages = new();
        Mock<IInitialPacketProcessor<TestPacket>> packets = new();
        EngineBuilder builder = EngineBuilder.Build(new TestEngineConfiguration(
            false,
            message => message.InitialProcessor<IInitialFrameProcessor<TestFrame>>(),
            packet => packet.InitialProcessor<IInitialPacketProcessor<TestPacket>>()));
        builder.Identify(connection => ((IIpConnectionInfo)connection).Host);
        EngineController controller = new(builder, new CurrentUserProvider(), null, Services(messages.Object, packets.Object));
        Mock<IInitialSession> session = new();
        TestFrame initialMessage = new();
        TestPacket initialPacket = new();

        Assert.Equal("10.0.0.1", controller.IdentifyConnection(info));
        controller.InitialFrameProcessor!.OnConnected(session.Object);
        controller.InitialFrameProcessor.OnInitial(session.Object, initialMessage);
        controller.InitialFrameProcessor.OnReply(session.Object, initialMessage);
        controller.InitialPacketProcessor!.OnInitial(session.Object, initialPacket);

        Assert.Equal(typeof(TestFrame), controller.InitialFrameProcessor.ItemType);
        Assert.Equal(typeof(TestPacket), controller.InitialPacketProcessor.ItemType);
        messages.Verify(m => m.OnConnected(It.IsAny<IInitialFrameContext<TestFrame>>()), Times.Once);
        messages.Verify(m => m.OnInitial(It.IsAny<IInitialFrameContext<TestFrame>>(), initialMessage), Times.Once);
        messages.Verify(m => m.OnReply(It.IsAny<IInitialFrameContext<TestFrame>>(), initialMessage), Times.Once);
        packets.Verify(p => p.OnInitial(It.IsAny<IInitialPacketContext<TestPacket>>(), initialPacket), Times.Once);
    }

    /// <summary>The context a processor is handed reflects the connection session it stands for.</summary>
    [Fact]
    public async Task InitialProcessor_Context_ReflectsTheSession()
    {
        Mock<IInitialSession> session = new();
        IpConnectionInfo info = new() { Host = "10.0.0.1", LocalUser = "ME" };
        Mock<IEngineContext> engine = new();
        engine.Setup(e => e.CurrentUser).Returns(new UserInfo { Name = "ME" });
        engine.Setup(e => e.IsConnected("BOB")).Returns(true);
        session.Setup(s => s.Engine).Returns(engine.Object);
        session.Setup(s => s.IsOpener).Returns(true);
        session.Setup(s => s.Connection).Returns(info);
        IInitialFrameContext<TestFrame>? seen = null;
        Mock<IInitialFrameProcessor<TestFrame>> processor = new();
        processor.Setup(p => p.OnConnected(It.IsAny<IInitialFrameContext<TestFrame>>())).Callback((IInitialFrameContext<TestFrame> context) => seen = context);
        EngineBuilder builder = EngineBuilder.Build(new TestEngineConfiguration(false, message => message.InitialProcessor<IInitialFrameProcessor<TestFrame>>()));
        EngineController controller = new(builder, new CurrentUserProvider(), null, Services(processor.Object));
        TestFrame sent = new() { Body = "HI" };

        controller.InitialFrameProcessor!.OnConnected(session.Object);

        Assert.NotNull(seen);
        Assert.True(seen.IsOpener);
        Assert.Equal("ME", seen.CurrentUser.Name);
        Assert.True(seen.IsConnected("BOB"));
        Assert.False(seen.IsConnected("X"));
        Assert.Same(info, seen.Connection);
        seen.Send(sent);
        seen.Connected("ALICE");
        seen.Disconnect();
        session.Verify(s => s.Send(sent), Times.Once);
        session.Verify(s => s.Connected("ALICE"), Times.Once);
        session.Verify(s => s.Disconnect(), Times.Once);
    }

    /// <summary>Without any initial exchange there is none, and the identification hook leaves the decision to the engine.</summary>
    [Fact]
    public void Unstated_Identification_IsOff()
    {
        (_, EngineController controller) = Build(engine => engine);

        Assert.Null(controller.IdentifyConnection(new IpConnectionInfo()));
        Assert.Null(controller.InitialPacketProcessor);
        Assert.Null(controller.InitialFrameProcessor);
    }

    /// <summary>External systems are reported in the order added, without duplicates, and the upstream hub is added if it was not already.</summary>
    [Fact]
    public void ExternalSystems_AddedOnceInOrder_AndServerIsAddedIfNew()
    {
        IExternalSystem first = Mock.Of<IExternalSystem>();
        IExternalSystem hub = Mock.Of<IExternalSystem>();
        (_, EngineController controller) = Build(engine => engine.ExternalSystem(first).ExternalSystem(first).ExternalServer(hub));

        Assert.Equal([first, hub], controller.ExternalSystems);
        Assert.Same(hub, controller.ExternalServer);
    }

    /// <summary>The network processor stated with the message is reported, and runs with contexts typed with the host's message.</summary>
    [Fact]
    public async Task NetworkProcessor_IsReported_AndGetsTypedContexts()
    {
        List<string> calls = [];
        Mock<INetworkProcessor<TestFrame>> processor = new();
        processor.Setup(p => p.OnConnected(It.IsAny<INetworkConnectedContext<TestFrame>>())).Callback((INetworkConnectedContext<TestFrame> context) => calls.Add($"connected:{context.TargetUser}"));
        processor.Setup(p => p.OnDisconnected(It.IsAny<INetworkDisconnectedContext<TestFrame>>())).Callback((INetworkDisconnectedContext<TestFrame> context) => calls.Add($"disconnected:{context.TargetUser}"));
        processor.Setup(p => p.OnReceived(It.IsAny<INetworkReceivedContext<TestFrame>>())).Callback((INetworkReceivedContext<TestFrame> context) => calls.Add($"received:{context.Frame.Body}"));
        (_, EngineController controller) = BuildWith(message => message.Processor<INetworkProcessor<TestFrame>>(), services: Services(processor.Object));
        Mock<INetworkUserContext> connection = new();
        connection.Setup(c => c.TargetUser).Returns("BOB");
        Mock<INetworkFrameContext> received = new();
        received.Setup(c => c.Frame).Returns(new TestFrame { Body = "HI" });

        controller.NetworkHandler!.OnConnected(connection.Object);
        controller.NetworkHandler.OnDisconnected(connection.Object);
        controller.NetworkHandler.OnReceived(received.Object);

        Assert.Equal(["connected:BOB", "disconnected:BOB", "received:HI"], calls);
    }

    private sealed class DependentProcessor(Dependency dependency) : INetworkProcessor<TestFrame>
    {
        public Dependency Dependency { get; } = dependency;

        public void OnConnected(INetworkConnectedContext<TestFrame> context) { }

        public void OnDisconnected(INetworkDisconnectedContext<TestFrame> context) { }

        public void OnReceived(INetworkReceivedContext<TestFrame> context) { }
    }

    /// <summary>A processor type that is not registered is constructed from the container's services, so its constructor can take dependencies.</summary>
    [Fact]
    public void Processor_Unregistered_IsConstructedWithInjectedServices()
    {
        ServiceCollection services = new();
        services.AddSingleton(new Dependency("INJECTED"));
        (_, EngineController controller) = BuildWith(message => message.Processor<DependentProcessor>(), services: services.BuildServiceProvider());

        Assert.NotNull(controller.NetworkHandler);
        Assert.Same(controller.NetworkHandler, controller.NetworkHandler);
    }

    private sealed class FirstExportFormat : IExportFormat
    {
        public string Name => "CSV";

        public Task Export(object entry, Stream stream, CancellationToken cancellation) => Task.CompletedTask;
    }

    private sealed class ReplacingExportFormat : IExportFormat
    {
        public string Name => "csv";

        public bool Accepts(FolderType type) => type == FolderType.Inbox;

        public Task Export(object entry, Stream stream, CancellationToken cancellation) => Task.CompletedTask;
    }

    private sealed class SlowImportFormat : IImportFormat
    {
        public string Name => "Slow";

        public StagedSendMode StagedSendMode => StagedSendMode.Simultaneous;

        public TimeSpan? StagedSendDelay => TimeSpan.FromSeconds(2);

        public Task Import(Stream stream, IImportFormatContext context, CancellationToken cancellation) => Task.CompletedTask;
    }

    /// <summary>Formats are added by type, a later format of the same name replaces an earlier one in place, and a format's own members become its definition.</summary>
    [Fact]
    public void Formats_AreAddedByType_AndSameNameReplaces()
    {
        (_, EngineController controller) = Build(engine => engine.ExportFormat<FirstExportFormat>().ExportFormat<ReplacingExportFormat>().ImportFormat<SlowImportFormat>());

        ExportFormatDefinition export = Assert.Single(controller.ExportFormats);
        Assert.Equal("csv", export.Name);
        Assert.True(export.AllowedTypes!(FolderType.Inbox));
        Assert.False(export.AllowedTypes(FolderType.Notes));
        ImportFormatDefinition import = Assert.Single(controller.ImportFormats);
        Assert.Equal(StagedSendMode.Simultaneous, import.StagedSendMode);
        Assert.Equal(TimeSpan.FromSeconds(2), import.StagedSendDelay);
    }

    /// <summary>Without a processor stated there is none.</summary>
    [Fact]
    public void NetworkProcessor_Unstated_IsNull()
    {
        (_, EngineController controller) = Build(engine => engine);

        Assert.Null(controller.NetworkHandler);
    }

    private sealed class Dependency(string name)
    {
        public string Name { get; } = name;
    }

    private sealed class InjectedConfiguration(Dependency dependency, ILoggerFactory loggers) : IEngineConfiguration
    {
        public IEngineBuilder Configure(IEngineBuilder engine) => new TestEngineConfiguration().Configure(engine)
            .AppName(dependency.Name)
            .HomeText(loggers is null ? "no logging" : "logging");
    }

    /// <summary>The configuration is constructed through dependency injection: it receives the services the host registered and the logging services the engine always provides.</summary>
    [Fact]
    public async Task Build_Generic_InjectsServicesIntoTheConfiguration()
    {
        await using EngineBuilder builder = EngineBuilder.Build<InjectedConfiguration>(services => services.AddSingleton(new Dependency("from-di")));

        Assert.Equal("from-di", builder.AppNameValue);
        Assert.Equal("logging", builder.HomeTextValue);
    }

    /// <summary>A configuration whose dependencies were not registered cannot be constructed, and fails with the container's own error.</summary>
    [Fact]
    public void Build_Generic_MissingDependency_Throws()
        => Assert.Throws<InvalidOperationException>(() => EngineBuilder.Build<InjectedConfiguration>(null));

    private sealed class PlainConfiguration : IEngineConfiguration
    {
        public IEngineBuilder Configure(IEngineBuilder engine) => new TestEngineConfiguration().Configure(engine);
    }

    /// <summary>A configuration with no dependencies needs no registrations at all.</summary>
    [Fact]
    public async Task Build_Generic_NoDependencies_NeedsNoRegistrations()
    {
        await using EngineBuilder builder = EngineBuilder.Build<PlainConfiguration>(null);

        Assert.NotNull(builder.FrameMap);
    }

    /// <summary>A configuration that turns out incomplete fails the build, after the container it was built in has been disposed.</summary>
    [Fact]
    public void Build_Generic_IncompleteConfiguration_Throws()
        => Assert.Throws<InvalidOperationException>(() => EngineBuilder.Build<EmptyConfiguration>(null));

    private sealed class DisposableService : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class DisposingConfiguration(DisposableService service) : IEngineConfiguration
    {
        public IEngineBuilder Configure(IEngineBuilder engine) => new TestEngineConfiguration().Configure(engine).CanDelete(_ => !service.IsDisposed);
    }

    /// <summary>The container the configuration was built in lives until the builder is disposed, since the configuration may have given the engine functions that use what was injected.</summary>
    [Fact]
    public async Task Build_Generic_KeepsTheContainerAliveUntilDisposed()
    {
        DisposableService service = new();
        EngineBuilder builder = EngineBuilder.Build<DisposingConfiguration>(services => services.AddSingleton(_ => service));
        Assert.True(builder.CanDeleteValue!(FolderType.Inbox));

        await builder.DisposeAsync();

        Assert.True(service.IsDisposed);
    }

    /// <summary>Every fluent call returns the builder, so a configuration can be one expression.</summary>
    [Fact]
    public void FluentCalls_ReturnTheBuilder()
    {
        EngineBuilder builder = new();

        Assert.Same(builder, builder.AppName("a").AppVersion("1").KioskMode().HomeText("h").CommandLineOverrides(false));
    }

    /// <summary>No server stores messages unless the configuration says so.</summary>
    [Fact]
    public void StorageServers_Unstated_IsEmpty()
    {
        (_, EngineController controller) = Build(engine => engine);

        Assert.Empty(controller.StorageServers);
    }

    /// <summary>Only server users whose entry says they store messages are storage servers.</summary>
    [Fact]
    public void StorageServers_AreTheServerUsersThatStoreMessages()
    {
        (_, EngineController controller) = Build(engine => engine, network: Network(
            ("Server1", new NetworkUserConfig { Role = "Server", StoresMessages = true }),
            ("Server2", new NetworkUserConfig { Role = "Server" }),
            ("Server3", new NetworkUserConfig { Role = "Server", StoresMessages = true }),
            ("Client1", new NetworkUserConfig { Role = "Client", StoresMessages = true })));

        Assert.Equal(["Server1", "Server3"], controller.StorageServers);
    }

    /// <summary>A user's security level is the one on their entry, or the lowest configured level when none is stated.</summary>
    [Fact]
    public void GetUserSecurityLevel_UsesTheEntryElseTheLowestLevel()
    {
        (_, EngineController controller) = Build(
            engine => engine.SecurityLevels(("LOW", "#111111"), ("HIGH", "#222222")),
            network: Network(("ALICE", new NetworkUserConfig { SecurityLevel = "HIGH" })));

        Assert.Equal("HIGH", controller.GetUserSecurityLevel("ALICE"));
        Assert.Equal("LOW", controller.GetUserSecurityLevel("BOB"));
    }
}
