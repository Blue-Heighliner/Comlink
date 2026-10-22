namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Control;

/// <summary>Unit tests for <see cref="EngineBuilder"/>: what a host states in its configuration is what <see cref="EngineController"/> reports, and anything left unstated takes the default.</summary>
public sealed class EngineBuilderTests
{
    private sealed class Configuration(Func<TestEngineBuilder, TestEngineBuilder> configure) : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine) => configure(new TestEngineConfiguration().Apply(engine));
    }

    private static IServiceProvider Services(params object[] handlers)
    {
        ServiceCollection services = new();
        foreach (object handler in handlers)
        {
            foreach (Type type in handler.GetType().GetInterfaces().Where(type => type.IsGenericType && type.Namespace == typeof(IPacketHandshakeHandler<>).Namespace && type.Name.EndsWith("Handler`1")))
            {
                services.AddSingleton(type, handler);
            }
        }

        return services.BuildServiceProvider();
    }

    private static (EngineBuilder Builder, EngineController Controller) Build(Func<TestEngineBuilder, TestEngineBuilder> configure, string? currentUser = null, NetworkConfig? network = null)
    {
        EngineBuilder builder = EngineBuilder.Build(new Configuration(configure));
        return (builder, new EngineController(builder, new CurrentUserProvider { UserName = currentUser }, network));
    }

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private static NetworkConfig Network(params (string Name, NetworkUserConfig User)[] users)
        => new() { Users = users.ToDictionary(user => user.Name, user => user.User) };

    /// <summary>A configuration that never states a frame type cannot start the engine.</summary>
    [Fact]
    public void Build_WithoutMessageType_Throws()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => EngineBuilder.Build(new EmptyConfiguration()));

        Assert.Contains("Types<", error.Message);
    }

    private sealed class TypesOnlyConfiguration : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine) => engine.Types<TestFrame, TestMessagePriority, TestLevel, TestAspect, NoProfile>().Priority(TestMessagePriority.Normal);
    }

    /// <summary>A configuration that states its types but no frame handlers cannot start the engine.</summary>
    [Fact]
    public void Build_TypesWithoutFrames_Throws()
        => Assert.Contains("Frames<THandler>()", Assert.Throws<InvalidOperationException>(() => EngineBuilder.Build(new TypesOnlyConfiguration())).Message);

    /// <summary>The types can only be stated once.</summary>
    [Fact]
    public void Types_StatedTwice_Throws()
    {
        EngineBuilder builder = new();
        builder.Types<TestFrame, TestMessagePriority, TestLevel, TestAspect, NoProfile>();

        Assert.Throws<InvalidOperationException>(() => builder.Types<TestFrame, TestMessagePriority, TestLevel, TestAspect, NoProfile>());
    }

    /// <summary>The no-priority and no-level types give a single NORMAL level and none, and packets cannot be stated without a packet type.</summary>
    [Fact]
    public void Types_NoPriorityAndNoLevel_AndPacketsWithoutAPacketType()
    {
        EngineBuilder builder = new();
        IEngineBuilder<TestFrame, NoPacket, NoPriority, NoLevel, NoAspect, NoProfile> typed = builder.Types<TestFrame, NoPriority, NoLevel, NoAspect, NoProfile>();

        Assert.Equal(["NORMAL"], builder.PriorityOptions.Select(option => option.Name));
        Assert.Empty(builder.LevelValues);
        Assert.Throws<InvalidOperationException>(() => typed.Packets<IPacketHandler<TestFrame, NoPacket>>(16 * 1024));
    }

    private sealed class UnorderedConfiguration : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine)
            => engine.Types<TestFrame, TestPacket, TestMessagePriority, TestLevel, TestAspect, NoProfile>()
                .Priority(TestMessagePriority.High).Priority(TestMessagePriority.Low).Priority(TestMessagePriority.Flash)
                .Level(TestLevel.Secret).Level(TestLevel.Public)
                .Frames<TestFrameHandler>()
                    .Heartbeat<TestHeartbeatHandler>();
    }

    private sealed class InterleavedConfiguration : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine)
            => engine.Types<TestFrame, TestPacket, TestMessagePriority, TestLevel, TestAspect, NoProfile>()
                .Priority(TestMessagePriority.High).Level(TestLevel.Secret).Priority(TestMessagePriority.Low).Label("LOW")
                .Level(TestLevel.Public).Priority(TestMessagePriority.High).Priority(TestMessagePriority.Flash).Level(TestLevel.Internal)
                .Frames<TestFrameHandler>();
    }

    /// <summary>Priorities and levels keep the order of their own calls however the calls are interleaved, and stating one again does not move it.</summary>
    [Fact]
    public void PrioritiesAndLevels_FollowTheOrderOfTheirCallsWhenInterleaved()
    {
        EngineController controller = new(EngineBuilder.Build(new InterleavedConfiguration()), new CurrentUserProvider(), null);

        Assert.Equal([TestMessagePriority.High, TestMessagePriority.Low, TestMessagePriority.Flash], controller.Priorities.Select(level => level.Key));
        Assert.Equal(["SECRET", "PUBLIC", "INTERNAL"], controller.Levels.Select(level => level.Label));
    }

    /// <summary>Levels rank in the order they are stated, not the order of the enum, a member not stated is not a level, and what is stored is the member's integer value.</summary>
    [Fact]
    public void Levels_FollowTheStatedOrderAndStoreTheEnumValue()
    {
        EngineBuilder builder = EngineBuilder.Build(new UnorderedConfiguration());
        EngineController controller = new(builder, new CurrentUserProvider(), null);

        Assert.Equal([TestMessagePriority.High, TestMessagePriority.Low, TestMessagePriority.Flash], controller.Priorities.Select(level => level.Key).Take(3));
        Assert.Equal(3, controller.Priorities.Count);
        Assert.Equal(["SECRET", "PUBLIC"], controller.Levels.Select(level => level.Label));
        Assert.Equal((int)TestMessagePriority.Flash, controller.StoredPriority(TestMessagePriority.Flash));
        Assert.Equal(TestMessagePriority.Flash, controller.PriorityOf((int)TestMessagePriority.Flash));
        Assert.Equal(TestMessagePriority.High, controller.PriorityOf(12345));
    }

    /// <summary>A handler naming a priority that is not a configured level fails the engine at startup, not later when something is sent.</summary>
    [Fact]
    public void Validate_HandlerPriorityNotConfigured_Throws()
    {
        EngineController controller = new(EngineBuilder.Build(new UnorderedConfiguration()), new CurrentUserProvider(), null);

        Assert.Contains("not one of the configured priorities", Assert.Throws<InvalidOperationException>(() => controller.Validate()).Message);
    }

    private sealed class DuplicateLabelConfiguration : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine)
            => engine.Types<TestFrame, TestPacket, TestMessagePriority, TestLevel, TestAspect, NoProfile>()
                .Priority(TestMessagePriority.Normal).Label("SAME").Priority(TestMessagePriority.Flash).Label("same")
                .Frames<TestFrameHandler>();
    }

    /// <summary>Two priorities with the same name are refused, since the name is what users pick by.</summary>
    [Fact]
    public void Build_DuplicatePriorityNames_Throws()
        => Assert.Contains("SAME", Assert.Throws<InvalidOperationException>(() => EngineBuilder.Build(new DuplicateLabelConfiguration())).Message);

    /// <summary>A level is accepted when it is a configured one, none is accepted as none, and anything else throws.</summary>
    [Fact]
    public void RequireLevel_NullIsNone_UnconfiguredThrows()
    {
        (_, EngineController controller) = Build(engine => engine);

        Assert.Null(controller.RequireLevel(null));
        Assert.Equal(TestLevel.Restricted, controller.RequireLevel(TestLevel.Restricted));
        Assert.Throws<ArgumentException>(() => controller.RequireLevel(TestMessagePriority.High));
    }

    /// <summary>The tag rules are the draft handler's, and unrestricted without one.</summary>
    [Fact]
    public void Drafts_HandlerStatesTheTagRules()
    {
        (_, EngineController none) = Build(engine => engine);
        (_, EngineController ruled) = Build(engine => engine.Drafts<TestTagRulesDraftHandler>());

        Assert.Equal(TagRules.Unrestricted, none.DraftTagRules);
        Assert.Equal(new TagRules(TagCase.Upper, 2, 6, false, true, false, true), ruled.DraftTagRules);
        Assert.Equal(DraftDefaults.None, none.DraftDefaults);
        Assert.Equal(new DraftDefaults("NEWTAG", TestMessagePriority.Level3, TestLevel.Restricted, null), ruled.DraftDefaults);
    }

    /// <summary>A priority enum other than the no-priority one needs its levels stated.</summary>
    [Fact]
    public void Build_WithoutPriorityLevels_Throws()
    {
        EngineBuilder builder = new();
        builder.Types<TestFrame, TestMessagePriority, TestLevel, TestAspect, NoProfile>();

        Assert.Throws<InvalidOperationException>(() => EngineBuilder.Build(new NoPrioritiesConfiguration()));
    }

    private sealed class NoPrioritiesConfiguration : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine) => engine.Types<TestFrame, TestMessagePriority, TestLevel, TestAspect, NoProfile>().Frames<TestFrameHandler>();
    }

    private sealed class EmptyConfiguration : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine) { }
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
        EngineBuilder builder = EngineBuilder.Build(new Configuration(engine => { calls++; return engine.CommandLineOverrides(true); }));

        Assert.Equal(1, calls);
        Assert.True(builder.AreCommandLineOverridesAllowed);
    }

    /// <summary>The data directory is the application's one folder under the application data root whoever the current user is, and the file that turns on log categories is in it.</summary>
    [Fact]
    public void AppDataPath_IsTheAppsOneFolder_WhoeverTheUserIs()
    {
        string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyApp");
        (_, EngineController noUser) = Build(engine => engine.Display<TestDisplayHandler>());
        (_, EngineController alice) = Build(engine => engine.Display<TestDisplayHandler>(), "ALICE");

        Assert.Equal(expected, noUser.AppDataPath);
        Assert.Equal(expected, alice.AppDataPath);
        Assert.Equal(Path.Combine(expected, "Logging.json"), alice.LoggingFilePath);
    }

    /// <summary>A data folder stated by the command line replaces the app's folder under the application data root for everything stored.</summary>
    [Fact]
    public void AppDataPath_DataFolderOverride_ReplacesTheAppsFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "node-data");
        (EngineBuilder builder, EngineController controller) = Build(engine => engine.Display<TestDisplayHandler>(), "ALICE");
        builder.DataFolderOverride = folder;

        Assert.Equal(folder, builder.AppDataFolder);
        Assert.Equal(folder, controller.AppDataPath);
        Assert.Equal(Path.Combine(folder, "Logging.json"), controller.LoggingFilePath);
    }

    /// <summary>The app's folder, where the network configuration file is read from, is the data folder name under the absolute application data root, never relative to the working directory.</summary>
    [Fact]
    public void AppDataFolder_IsAbsoluteUnderTheApplicationDataRoot()
    {
        (EngineBuilder builder, _) = Build(engine => engine.Display<TestDisplayHandler>());

        Assert.True(Path.IsPathRooted(builder.AppDataFolder));
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyApp"), builder.AppDataFolder);
    }

    /// <summary>Without a draft handler no combination is blocked, and a stated handler is asked with the host's own enum members and gives its reason.</summary>
    [Fact]
    public void DraftHandler_DecidesWhichCombinationsAreBlocked()
    {
        Mock<IDraftHandler<TestMessagePriority, TestLevel, TestAspect>> handler = new() { CallBase = true };
        handler.Setup(b => b.IsBlocked(It.Is<IDraftBlockContext<TestMessagePriority, TestLevel, TestAspect>>(context => context.Priority == TestMessagePriority.High && context.Level == TestLevel.Secret && context.Aspect == TestAspect.Signed && context.Tag == "SPAM"))).Returns("No spam at the top");
        ServiceCollection services = new();
        services.AddSingleton(handler.Object);
        EngineBuilder builder = EngineBuilder.Build(new Configuration(engine => engine.Drafts<IDraftHandler<TestMessagePriority, TestLevel, TestAspect>>()));
        EngineController stated = new(builder, new CurrentUserProvider(), null, services.BuildServiceProvider());
        (_, EngineController plain) = Build(engine => engine);

        Assert.Equal("No spam at the top", stated.IsDraftBlocked(TestMessagePriority.High, TestLevel.Secret, TestAspect.Signed, "SPAM"));
        Assert.Null(stated.IsDraftBlocked(TestMessagePriority.High, null, null, "OK"));
        Assert.Null(plain.IsDraftBlocked(TestMessagePriority.High, TestLevel.Secret, TestAspect.Signed, "SPAM"));
    }

    /// <summary>Without an info handler every name and word is the engine's own, and a handler renames what it states.</summary>
    [Fact]
    public void Display_StatedHandlerRenamesAndUnstatedKeepsDefaults()
    {
        (_, EngineController plain) = Build(engine => engine);
        (_, EngineController named) = Build(engine => engine.Display<TestDisplayHandler>());

        Assert.Equal(["HOME", "ALERT", "Tag", "Priority", "Level", "Inbox"], [plain.HomeText, plain.AlertLabel, plain.TagLabel, plain.PriorityLabel, plain.LevelLabel, plain.Rename("Inbox")]);
        Assert.Equal(["Welcome", "ALARM", "Category", "Importance", "Classification", "Received", "Outbox"], [named.HomeText, named.AlertLabel, named.TagLabel, named.PriorityLabel, named.LevelLabel, named.Rename("Inbox"), named.Rename("Outbox")]);
    }

    /// <summary>Fixed interface text written with the engine's own names shows what the host calls the concepts, in the case style of what it replaces, and is untouched when the host states nothing.</summary>
    [Fact]
    public void Display_ReplacesConceptNamesKeepingCaseStyle()
    {
        (_, EngineController plain) = Build(engine => engine);
        (_, EngineController named) = Build(engine => engine.Display<TestDisplayHandler>());

        Assert.Equal("NEW DRAFT in the Inbox, Alert only", plain.Display("NEW DRAFT in the Inbox, Alert only"));
        Assert.Equal("NEW DRAFT in the Received, ALARM only", named.Display("NEW DRAFT in the Inbox, Alert only"));
        Assert.Equal("Categories, importances and classifications, plus alarms.", named.Display("Tags, priorities and levels, plus alerts."));
        Assert.Equal("Choose an importance, classification and category.", named.Display("Choose an priority, level and tag."));
        Assert.Equal("No such user. Users are listed.", plain.Display("No such user. Users are listed."));
        Assert.Equal("No such operator. Operators are listed.", named.Display("No such user. Users are listed."));
        Assert.Equal("NO SUCH OPERATOR", named.Display("NO SUCH USER"));
    }

    /// <summary>The separated alert folders have labels of their own, and without one are the alert label and the inbox or outbox label in place of the words they name.</summary>
    [Fact]
    public void Display_AlertFolders_UseTheirOwnLabelOrComposeTheOthers()
    {
        (_, EngineController plain) = Build(engine => engine);
        (_, EngineController named) = Build(engine => engine.Display<TestDisplayHandler>());

        Assert.Equal(["Alert Inbox", "Alert Outbox"], [plain.Display("Alert Inbox"), plain.Display("Alert Outbox")]);
        Assert.Equal(["ALARM Received", "Flagged", "FLAGGED", "alarm received"], [named.Display("Alert Inbox"), named.Display("Alert Outbox"), named.Display("ALERT OUTBOX"), named.Display("alert inbox")]);
        Assert.Equal(["Received", "Flagged"], [named.Rename("Inbox"), named.Rename("Alert Outbox")]);
    }

    /// <summary>An overridden address type label replaces the default for that type only; the others keep theirs.</summary>
    [Fact]
    public void AddressTypes_Labelled_ReplacesOnlyThatTypesDefault()
    {
        (_, EngineController controller) = Build(engine => engine.AddressType(AddressType.External).Label("OUTSIDE"));

        Assert.Equal(["To", "Cc", "OUTSIDE"], controller.AddressTypes.Select(t => t.Label));
        Assert.Equal([AddressType.To, AddressType.Cc, AddressType.External], controller.AddressTypes.Select(t => t.Type));
    }

    /// <summary>The maximum payload size is the argument of Packets and the window is stated on the packet configuration, defaulting to 1.</summary>
    [Fact]
    public void MaxPayloadSizeAndWindow_AreStatedOnPackets()
    {
        (_, EngineController stated) = Build(engine => engine.Packets<TestPacketHandler>(1024).Window(3));
        (_, EngineController unstated) = Build(engine => engine);

        Assert.Equal((1024, 3), (stated.MaxPayloadSize, stated.PacketWindow));
        Assert.Equal((0, 1), (unstated.MaxPayloadSize, unstated.PacketWindow));
    }

    /// <summary>Stating priorities twice replaces the earlier list rather than adding to it.</summary>
    [Fact]
    public void Priorities_StatedTwice_ReplacesTheEarlierList()
    {
        (_, EngineController controller) = Build(engine => engine
            .Priority(TestMessagePriority.Flash).Label("FIRST")
            .Priority(TestMessagePriority.Flash).Label("SECOND"));

        Assert.Contains("SECOND", controller.Priorities.Select(p => p.Name));
        Assert.DoesNotContain("FIRST", controller.Priorities.Select(p => p.Name));
    }

    /// <summary>Users and groups come from the network file, and the data attached to a user comes from their entry.</summary>
    [Fact]
    public void NetworkUsersAndGroups_AreReported()
    {
        (_, EngineController controller) = Build(engine => engine,
            network: new NetworkConfig
            {
                Users = { ["alice"] = new NetworkUserConfig(), ["BOB"] = new NetworkUserConfig(), ["DAVE"] = new NetworkUserConfig() },
                UserGroups = { ["OPS"] = ["ALICE", "BOB", "DAVE"], ["EXTRA"] = ["DAVE"] }
            });

        Assert.Equal(["alice", "BOB", "DAVE", "OPS", "EXTRA"], controller.Users);
        Assert.Equal(["ALICE", "BOB", "DAVE"], controller.UserGroups["ops"]);
        Assert.Equal(["DAVE"], controller.UserGroups["extra"]);
        Assert.Equal(["OPS", "EXTRA"], controller.GetUserInfo("DAVE").Groups);
    }

    /// <summary>A user the network does not list is just a name; a listed user's details are what the file says.</summary>
    [Fact]
    public void GetUserInfo_ReturnsTheNetworksDetailsOrJustTheName()
    {
        (_, EngineController controller) = Build(engine => engine, network: Network(("ALICE", new NetworkUserConfig { LevelValue = TestLevel.High })));

        Assert.Equal(TestLevel.High, controller.GetUserInfo("alice").Level);
        IUserInfo other = controller.GetUserInfo("BOB");
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
            IpHost = "10.0.0.9",
            Msmt = Json("""{ "Port": 1234 }"""),
            Hdlc = Json("""{ "Address": 1, "Ports": [ "SL0", "SL1" ] }"""),
            InterfacePort = 5678,
            Parent = "UPSTREAM",
            Children = ["C1", new NetworkLinkConfig { User = "C2", Mode = "MsmtConnect" }, new NetworkLinkConfig { User = "C3", Mode = "Hdlc" }, new NetworkLinkConfig { User = "C4", Mode = "Hdlc" }]
        }),
        ("UPSTREAM", new NetworkUserConfig { Role = "Server", IpHost = "10.0.0.1", Msmt = Json("""{ "Port": 1 }""") }),
        ("C2", new NetworkUserConfig { Role = "Client", IpHost = "10.0.0.2", Msmt = Json("""{ "Port": 2 }""") }),
        ("C3", new NetworkUserConfig { Role = "Client", Hdlc = Json("""{ "Address": 2 }""") }),
        ("C4", new NetworkUserConfig { Role = "Client", Hdlc = Json("""{ "Address": 4 }""") }));
        (_, EngineController server) = Build(engine => engine, "SERVER", network);
        (_, EngineController noInfo) = Build(engine => engine, "OTHER", network);
        (_, EngineController noUser) = Build(engine => engine, network: network);

        Assert.Equal((UserRole.Server, 1234, 5678), (server.Role, server.PeerPort, server.InterfacePort));
        Assert.Equal(
            [new ConnectionPoint { IpAddress = "10.0.0.1", Port = 1 }, new ConnectionPoint { IpAddress = "10.0.0.2", Port = 2 }, new ConnectionPoint { SerialPort = "SL0", SerialAddress = 1, RemoteSerialAddress = 2, User = "C3" }, new ConnectionPoint { SerialPort = "SL1", SerialAddress = 1, RemoteSerialAddress = 2, User = "C3" }],
            server.OutgoingPoints);
        Assert.Equal([new HdlcRemote("C4", 4)], server.OutgoingPoints.Last().OtherRemotes);
        Assert.Equal("C4", server.OutgoingPoints.Last().UserAt(4));
        Assert.Equal("C3", server.OutgoingPoints.Last().UserAt(2));
        Assert.Equal("UPSTREAM", server.ParentUser);
        Assert.Equal([new ConnectionPoint { IpAddress = "10.0.0.1", Port = 1 }], server.ParentPoints);
        Assert.Equal(["C1", "C2", "C3", "C4"], server.Servers["server"].Children);
        Assert.All([noInfo, noUser], controller =>
        {
            Assert.Equal((UserRole.Client, 50021, 50020), (controller.Role, controller.PeerPort, controller.InterfacePort));
            Assert.Empty(controller.OutgoingPoints);
            Assert.Equal(2, controller.Servers.Count);
        });
    }

    /// <summary>HDLC stations linked together must have different addresses: the local and remote address may not match, nor may two remotes share one.</summary>
    [Fact]
    public void HdlcLinks_WithTheSameAddress_AreAnError()
    {
        NetworkConfig sameAsLocal = Network(
            ("SERVER", new NetworkUserConfig { Role = "Server", Hdlc = Json("""{ "Address": 3, "Ports": [ "SL0" ] }"""), Children = [new NetworkLinkConfig { User = "C1", Mode = "Hdlc" }] }),
            ("C1", new NetworkUserConfig { Role = "Client", Hdlc = Json("""{ "Address": 3 }""") }));
        NetworkConfig defaults = Network(
            ("SERVER", new NetworkUserConfig { Role = "Server", Hdlc = Json("""{ "Ports": [ "SL0" ] }"""), Children = [new NetworkLinkConfig { User = "C1", Mode = "Hdlc" }] }),
            ("C1", new NetworkUserConfig { Role = "Client" }));
        NetworkConfig sharedRemote = Network(
            ("SERVER", new NetworkUserConfig { Role = "Server", Hdlc = Json("""{ "Address": 1, "Ports": [ "SL0" ] }"""), Children = [new NetworkLinkConfig { User = "C1", Mode = "Hdlc" }, new NetworkLinkConfig { User = "C2", Mode = "Hdlc" }] }),
            ("C1", new NetworkUserConfig { Role = "Client", Hdlc = Json("""{ "Address": 2 }""") }),
            ("C2", new NetworkUserConfig { Role = "Client", Hdlc = Json("""{ "Address": 2 }""") }));

        Assert.Contains("must differ", Assert.Throws<InvalidOperationException>(() => Build(engine => engine, "SERVER", sameAsLocal).Controller.OutgoingPoints).Message);
        Assert.Contains("must differ", Assert.Throws<InvalidOperationException>(() => Build(engine => engine, "SERVER", defaults).Controller.OutgoingPoints).Message);
        Assert.Contains("each user needs its own", Assert.Throws<InvalidOperationException>(() => Build(engine => engine, "SERVER", sharedRemote).Controller.OutgoingPoints).Message);
    }

    /// <summary>A parent is dialed by default and a child listened for; a forced mode reverses that, and listening for a parent leaves no point to dial.</summary>
    [Fact]
    public void Links_DefaultToConnectingToParentsAndListeningForChildren_UnlessForced()
    {
        NetworkConfig network = Network(
            ("PARENT", new NetworkUserConfig { Role = "Server", IpHost = "10.0.0.1", Msmt = Json("""{ "Port": 1 }""") }),
            ("CHILD", new NetworkUserConfig { Role = "Client", IpHost = "10.0.0.2", Msmt = Json("""{ "Port": 2 }"""), Parent = "PARENT" }),
            ("LISTENER", new NetworkUserConfig { Role = "Client", Parent = new NetworkLinkConfig { User = "PARENT", Mode = "MsmtListen" } }));
        (_, EngineController child) = Build(engine => engine, "CHILD", network);
        (_, EngineController listener) = Build(engine => engine, "LISTENER", network);

        Assert.Equal([new ConnectionPoint { IpAddress = "10.0.0.1", Port = 1 }], child.ParentPoints);
        Assert.Equal("PARENT", listener.ParentUser);
        Assert.Empty(listener.ParentPoints);
        Assert.Empty(listener.OutgoingPoints);
    }

    /// <summary>Each MSMT call sets only its own option, so the calls compose in any order and the options not stated keep the values the engine built.</summary>
    [Fact]
    public void Msmt_EachCallSetsItsOwnOption()
    {
        MsmtSessionPeerOptions options = new() { Credentials = new MsmtCredentials { Identity = TestMsmtCertificates.Create().Server, TrustedAuthorities = [] } };
        (_, EngineController controller) = Build(engine => engine
            .Msmt().KeepAliveMaxInterval(TimeSpan.FromMinutes(9)).StallTimeout(null).ResponseTimeout(TimeSpan.FromSeconds(3)).HandshakeTimeout(TimeSpan.FromSeconds(4))
            .TcpKeepAliveTime(TimeSpan.FromSeconds(5)).MaximumSessionLifetime(TimeSpan.FromMinutes(6)).SessionLifetime(TimeSpan.FromMinutes(7)).KeepAliveMinInterval(TimeSpan.FromMinutes(8)));

        MsmtSessionPeerOptions applied = controller.ConfigureConnectionOptions(options);

        Assert.Equal(
            new TimeSpan?[] { TimeSpan.FromSeconds(4), null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5) },
            new TimeSpan?[] { applied.HandshakeTimeout, applied.StallTimeout, applied.ResponseTimeout, applied.TcpKeepAliveTime });
        Assert.Equal(
            [TimeSpan.FromMinutes(6), TimeSpan.FromMinutes(7), TimeSpan.FromMinutes(8), TimeSpan.FromMinutes(9)],
            [applied.MaximumSessionLifetime, applied.SessionLifetime, applied.KeepAliveMinInterval, applied.KeepAliveMaxInterval]);
        Assert.Same(options.Credentials, applied.Credentials);
    }

    /// <summary>The HDLC calls that name a line setting change the link options and the others change the peer options, each leaving every other option at its default.</summary>
    [Fact]
    public void Hdlc_LineAndPeerOptions_ComposeAcrossCalls()
    {
        HdlcPeerOptions defaults = new();
        (_, EngineController controller) = Build(engine => engine
            .Hdlc().ClockSpeed(9600).MaxInfoField(256).Crc(HdlcCrc.Crc16Ccitt).TransmitWindow(2).Loopback(true).DetectDisconnect(false));

        HdlcPeerOptions options = controller.HdlcOptions;

        Assert.Equal(9600, options.Link.ClockSpeed);
        Assert.Equal(HdlcCrc.Crc16Ccitt, options.Link.Crc);
        Assert.Equal([256, 2], [options.MaxInfoField, options.TransmitWindow]);
        Assert.Equal([true, false], [options.Loopback, options.DetectDisconnect]);
        Assert.Equal(defaults.Link.Encoding, options.Link.Encoding);
        Assert.Equal(defaults.AcknowledgeDelay, options.AcknowledgeDelay);
    }

    /// <summary>A maximum payload size below one is refused when stated.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Packets_NonPositiveMaxPayloadSize_Throws(int size)
    {
        EngineBuilder builder = new();
        IEngineBuilder<TestFrame, TestPacket, TestMessagePriority, TestLevel, TestAspect, NoProfile> typed = builder.Types<TestFrame, TestPacket, TestMessagePriority, TestLevel, TestAspect, NoProfile>();

        Assert.Throws<ArgumentOutOfRangeException>(() => typed.Packets<TestPacketHandler>(size));
    }

    /// <summary>A packet heartbeat handler takes precedence over a frame one, for the intervals as well as for what is sent.</summary>
    [Fact]
    public void PacketHeartbeat_TakesPrecedenceOverTheFrameHeartbeat()
    {
        ServiceCollection services = new();
        services.AddSingleton(new TestHeartbeatHandler { Interval = TimeSpan.FromSeconds(5), RetryInterval = TimeSpan.FromSeconds(1) });
        services.AddSingleton(new TestPacketHeartbeatHandler { Interval = TimeSpan.FromSeconds(11), RetryInterval = TimeSpan.FromSeconds(7) });
        EngineBuilder builder = EngineBuilder.Build(new TestEngineConfiguration(packets: true, packetExtra: packet => packet.Heartbeat<TestPacketHeartbeatHandler>()));
        EngineController controller = new(builder, new CurrentUserProvider(), null, services.BuildServiceProvider());

        Assert.Equal([TimeSpan.FromSeconds(11), TimeSpan.FromSeconds(7)], [controller.HeartbeatInterval, controller.HeartbeatRetryInterval]);
    }

    /// <summary>The stated MSMT options are used with the engine's own credentials, and the built options are used as they are when none are stated.</summary>
    [Fact]
    public void Stated_MsmtOptions_ReplaceTheSettingsUsedForEveryConnectionKeepingTheEnginesCredentials()
    {
        MsmtSessionPeerOptions options = new() { Credentials = new MsmtCredentials { Identity = TestMsmtCertificates.Create().Server, TrustedAuthorities = [] } };
        (_, EngineController adjusted) = Build(engine => engine
            .Msmt().HandshakeTimeout(TimeSpan.FromSeconds(7)));
        (_, EngineController plain) = Build(engine => engine);

        MsmtSessionPeerOptions applied = adjusted.ConfigureConnectionOptions(options);
        Assert.Equal(TimeSpan.FromSeconds(7), applied.HandshakeTimeout);
        Assert.Same(options.Credentials, applied.Credentials);
        Assert.Equal(options.HandshakeTimeout, plain.ConfigureConnectionOptions(options).HandshakeTimeout);
    }

    /// <summary>The stated MicroGate options are used, and the defaults when none are stated.</summary>
    [Fact]
    public void Stated_HdlcOptions_ReplaceTheDefaults()
    {
        (_, EngineController adjusted) = Build(engine => engine
            .Hdlc().MaxInfoField(512).Crc(HdlcCrc.Crc16Ccitt));
        (_, EngineController plain) = Build(engine => engine);

        Assert.Equal(512, adjusted.HdlcOptions.MaxInfoField);
        Assert.Equal(HdlcCrc.Crc16Ccitt, adjusted.HdlcOptions.Link.Crc);
        Assert.Equal(new HdlcPeerOptions(), plain.HdlcOptions);
    }

    private sealed class ProfileHandler : IFrameHandler<TestFrame, TestMessagePriority, TestLevel, TestAspect>;

    private sealed class ProfileConfiguration : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine) => engine.Types<TestFrame, TestPacket, TestMessagePriority, TestLevel, TestAspect, TestProfile>().Priority(TestMessagePriority.Normal).Frames<ProfileHandler>();
    }

    /// <summary>The profile type a host states in its Types call is recorded for the network configuration to read profiles into, and there is none when it states NoProfile.</summary>
    [Fact]
    public void Types_RecordTheProfileType()
    {
        (EngineBuilder none, _) = Build(engine => engine);
        EngineBuilder stated = EngineBuilder.Build(new ProfileConfiguration());

        Assert.Null(none.UserProfileType);
        Assert.Equal(typeof(TestProfile), stated.UserProfileType);
    }

    /// <summary>A log handler fixes the widths of the fields it states, and without one no field is fixed.</summary>
    [Fact]
    public void LogHandler_FixesTheStatedWidths()
    {
        (_, EngineController none) = Build(engine => engine);
        (_, EngineController fixedWidths) = Build(engine => engine.Logs<TestLogHandler>());

        Assert.Equal(LogFieldWidths.None, none.LogWidths);
        Assert.Equal(new LogFieldWidths(8, 7, 3), fixedWidths.LogWidths);
    }

    /// <summary>Without a stated list the engine's own categories are on by default, and a log handler's list replaces it.</summary>
    [Fact]
    public void LogHandler_StatesTheCategoriesOnByDefault()
    {
        Mock<ILogHandler> handler = new() { CallBase = true };
        handler.SetupGet(h => h.CategoriesOnByDefault).Returns(["frames", "Mine"]);
        ServiceCollection services = new();
        services.AddSingleton(handler.Object);
        EngineBuilder builder = EngineBuilder.Build(new Configuration(engine => engine.Logs<ILogHandler>()));
        EngineController stated = new(builder, new CurrentUserProvider(), null, services.BuildServiceProvider());
        (_, EngineController none) = Build(engine => engine);
        (_, EngineController unstated) = Build(engine => engine.Logs<TestLogHandler>());

        Assert.Equal(LogCategories.OnByDefault, none.LogCategoriesOnByDefault);
        Assert.Equal(LogCategories.OnByDefault, unstated.LogCategoriesOnByDefault);
        Assert.Equal(["frames", "Mine"], stated.LogCategoriesOnByDefault);
    }

    /// <summary>The packet handshake handler is reported and used.</summary>
    [Fact]
    public async Task Stated_PacketHandshake_IsUsed()
    {
        Mock<IPacketHandshakeHandler<TestPacket>> packets = new();
        EngineBuilder builder = EngineBuilder.Build(new TestEngineConfiguration(false, null, packet => packet.Handshake<IPacketHandshakeHandler<TestPacket>>()));
        EngineController controller = new(builder, new CurrentUserProvider(), null, Services(packets.Object));
        Mock<IHandshakeSession> session = new();
        TestPacket received = new();

        await controller.PacketHandshakeHandler!.OnConnected(session.Object);
        await controller.PacketHandshakeHandler.OnReceived(session.Object, received);

        Assert.Equal(typeof(TestPacket), controller.PacketHandshakeHandler.ItemType);
        packets.Verify(p => p.OnConnected(It.IsAny<IPacketHandshakeContext<TestPacket>>()), Times.Once);
        packets.Verify(p => p.OnReceived(It.IsAny<IPacketHandshakeContext<TestPacket>>(), received), Times.Once);
    }

    /// <summary>The frame handshake handler is reported and used, apart from the packet one.</summary>
    [Fact]
    public async Task Stated_FrameHandshake_IsUsed()
    {
        Mock<IFrameHandshakeHandler<TestFrame>> frames = new();
        EngineBuilder builder = EngineBuilder.Build(new TestEngineConfiguration(false, frame => frame.Handshake<IFrameHandshakeHandler<TestFrame>>()));
        EngineController controller = new(builder, new CurrentUserProvider(), null, Services(frames.Object));
        Mock<IHandshakeSession> session = new();
        TestFrame received = new();

        await controller.FrameHandshakeHandler!.OnConnected(session.Object);
        await controller.FrameHandshakeHandler.OnReceived(session.Object, received);

        Assert.Equal(typeof(TestFrame), controller.FrameHandshakeHandler.ItemType);
        Assert.Null(controller.PacketHandshakeHandler);
        frames.Verify(p => p.OnConnected(It.IsAny<IFrameHandshakeContext<TestFrame>>()), Times.Once);
        frames.Verify(p => p.OnReceived(It.IsAny<IFrameHandshakeContext<TestFrame>>(), received), Times.Once);
    }

    /// <summary>The context a handler is handed reflects the connection session it stands for.</summary>
    [Fact]
    public async Task Handshake_Context_ReflectsTheSession()
    {
        Mock<IHandshakeSession> session = new();
        IpConnectionInfo info = new() { Host = "10.0.0.1", LocalUser = "ME" };
        session.Setup(s => s.Connection).Returns(info);
        IPacketHandshakeContext<TestPacket>? seen = null;
        Mock<IPacketHandshakeHandler<TestPacket>> handler = new();
        handler.Setup(p => p.OnConnected(It.IsAny<IPacketHandshakeContext<TestPacket>>())).Callback((IPacketHandshakeContext<TestPacket> context) => seen = context);
        EngineBuilder builder = EngineBuilder.Build(new TestEngineConfiguration(false, null, packet => packet.Handshake<IPacketHandshakeHandler<TestPacket>>()));
        EngineController controller = new(builder, new CurrentUserProvider(), null, Services(handler.Object));
        TestPacket sent = new() { PayloadId = 5 };

        await controller.PacketHandshakeHandler!.OnConnected(session.Object);

        Assert.NotNull(seen);
        Assert.Same(info, seen.Connection);
        await seen.Send(sent);
        await seen.Accept("ALICE");
        await seen.Reject("not trusted");
        session.Verify(s => s.Send(sent), Times.Once);
        session.Verify(s => s.Accept("ALICE"), Times.Once);
        session.Verify(s => s.Reject("not trusted"), Times.Once);
    }

    /// <summary>External systems are reported in the order added, without duplicates.</summary>
    [Fact]
    public void ExternalSystems_AddedOnceInOrder()
    {
        IExternalSystem first = Mock.Of<IExternalSystem>();
        IExternalSystem second = Mock.Of<IExternalSystem>();
        (_, EngineController controller) = Build(engine => engine.ExternalSystem(first).ExternalSystem(first).ExternalSystem(second));

        Assert.Equal([first, second], controller.ExternalSystems);
    }

    private sealed class DependentHandler(Dependency dependency) : IFrameHandler<TestFrame, TestMessagePriority, TestLevel, TestAspect>
    {
        public Dependency Dependency { get; } = dependency;

        public Task OnConnected(INetworkConnectedContext context) => Task.CompletedTask;

        public Task OnDisconnected(INetworkDisconnectedContext context) => Task.CompletedTask;

        public Task OnReceived(INetworkReceivedContext<TestFrame> context) => Task.CompletedTask;
    }

    private sealed class DependentHandlerConfiguration : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine)
            => engine.Types<TestFrame, TestPacket, TestMessagePriority, TestLevel, TestAspect, NoProfile>()
                .Priority(TestMessagePriority.Normal)
                .Frames<DependentHandler>();
    }

    /// <summary>A handler type that is not registered is constructed from the container's services, so its constructor can take dependencies.</summary>
    [Fact]
    public void Handler_Unregistered_IsConstructedWithInjectedServices()
    {
        ServiceCollection services = new();
        services.AddSingleton(new Dependency("INJECTED"));
        EngineBuilder builder = EngineBuilder.Build(new DependentHandlerConfiguration());
        EngineController controller = new(builder, new CurrentUserProvider(), null, services.BuildServiceProvider());

        Assert.NotNull(controller.FrameHandler);
        Assert.Same(controller.FrameHandler, controller.FrameHandler);
    }

    private sealed class PropertyDisplayHandler : IDisplayHandler
    {
        public required Dependency Dependency { get; init; }

        public string? AppName => Dependency.Name;
    }

    /// <summary>A handler takes what it needs as required init properties as well as constructor arguments, which the engine fills in from the container.</summary>
    [Fact]
    public void Handler_RequiredInitProperties_AreFilledFromTheContainer()
    {
        ServiceCollection services = new();
        services.AddSingleton(new Dependency("PROPERTY-INJECTED"));

        EngineBuilder builder = EngineBuilder.Build(new Configuration(engine => engine.Display<PropertyDisplayHandler>()), services.BuildServiceProvider());

        Assert.Equal("PROPERTY-INJECTED", builder.DisplayHandlerInstance!.AppName);
    }

    private sealed class PropertyConfigurationHandler : IConfigurationHandler
    {
        public required Dependency Dependency { get; init; }

        public int Calls { get; private set; }

        public Task OnReconfigured()
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    /// <summary>Without a configuration handler none is reported, and a stated one is built through dependency injection, constructor and required properties alike, once.</summary>
    [Fact]
    public async Task Initialize_StatesTheInitializer_BuiltFromTheContainer()
    {
        ServiceCollection services = new();
        services.AddSingleton(new Dependency("INITIAL"));
        EngineBuilder builder = EngineBuilder.Build(new Configuration(engine => engine.Configure<PropertyConfigurationHandler>()));
        EngineController stated = new(builder, new CurrentUserProvider(), null, services.BuildServiceProvider());
        (_, EngineController plain) = Build(engine => engine);

        PropertyConfigurationHandler configurationHandler = Assert.IsType<PropertyConfigurationHandler>(stated.ConfigurationHandler);
        await configurationHandler.OnReconfigured();

        Assert.Equal(("INITIAL", 1), (configurationHandler.Dependency.Name, configurationHandler.Calls));
        Assert.Same(configurationHandler, stated.ConfigurationHandler);
        Assert.Null(plain.ConfigurationHandler);
    }

    private sealed class DefaultIdentifier : IConfigurationHandler
    {
        public required Dependency Dependency { get; init; }
    }

    private sealed class LastNameIdentifier(Dependency dependency) : IConfigurationHandler
    {
        public int Calls { get; private set; }

        public string Identify(IReadOnlyList<string> commonNames)
        {
            Calls++;
            return dependency.Name + commonNames[^1];
        }
    }

    private sealed class ExplicitIdentifier : IConfigurationHandler
    {
        string IConfigurationHandler.Identify(IReadOnlyList<string> commonNames) => "EXPLICIT";
    }

    /// <summary>Without a handler, or with one that does not state its own Identify, the first common name is the user and the handler is not built at all (it could not be, here, without its dependency).</summary>
    [Fact]
    public void Identify_WithoutAnOverride_IsTheFirstCommonName_AndBuildsNothing()
    {
        EngineBuilder none = EngineBuilder.Build(new Configuration(engine => engine), new ServiceCollection().BuildServiceProvider());
        EngineBuilder notIdentifying = EngineBuilder.Build(new Configuration(engine => engine.Configure<DefaultIdentifier>()), new ServiceCollection().BuildServiceProvider());

        Assert.Equal("A", none.Identify(["A", "B"]));
        Assert.Equal("A", notIdentifying.Identify(["A", "B"]));
    }

    /// <summary>A handler that states Identify is built once from the bootstrap container and asked each time, and an explicit interface implementation counts as stating it.</summary>
    [Fact]
    public void Identify_WithAnOverride_AsksTheHandlerBuiltOnceFromTheBootstrapContainer()
    {
        ServiceCollection services = new();
        services.AddSingleton(new Dependency("USER-"));
        EngineBuilder builder = EngineBuilder.Build(new Configuration(engine => engine.Configure<LastNameIdentifier>()), services.BuildServiceProvider());
        EngineBuilder explicitBuilder = EngineBuilder.Build(new Configuration(engine => engine.Configure<ExplicitIdentifier>()), services.BuildServiceProvider());

        Assert.Equal("USER-B", builder.Identify(["A", "B"]));
        Assert.Equal("USER-C", builder.Identify(["C"]));
        Assert.Equal("EXPLICIT", explicitBuilder.Identify(["A"]));
    }

    private sealed class FirstExportFormat : IExportFormat
    {
        public string Name => "CSV";

        public Task Export(object entry, Stream stream, CancellationToken cancellation) => Task.CompletedTask;
    }

    private sealed class ReplacingExportFormat : IExportFormat
    {
        public string Name => "csv";

        public bool Accepts(FolderType type) => type is FolderType.Inbox;

        public Task Export(object entry, Stream stream, CancellationToken cancellation) => Task.CompletedTask;
    }

    private sealed class SlowImportFormat : IImportFormat<TestMessagePriority, TestLevel>
    {
        public string Name => "Slow";

        public StagedSendMode StagedSendMode => StagedSendMode.Simultaneous;

        public TimeSpan? StagedSendDelay => TimeSpan.FromSeconds(2);

        public Task Import(Stream stream, IImportFormatContext<TestMessagePriority, TestLevel> context, CancellationToken cancellation) => Task.CompletedTask;
    }

    /// <summary>Formats are added by type, a later format of the same name replaces an earlier one in place, and a format's own members become its definition.</summary>
    [Fact]
    public void Formats_AreAddedByType_AndSameNameReplaces()
    {
        (_, EngineController controller) = Build(engine => engine.Export<FirstExportFormat>().Export<ReplacingExportFormat>().Import<SlowImportFormat>());

        ExportFormatDefinition export = Assert.Single(controller.ExportFormats);
        Assert.Equal("csv", export.Name);
        Assert.True(export.AllowedTypes!(FolderType.Inbox));
        Assert.False(export.AllowedTypes(FolderType.Notes));
        ImportFormatDefinition import = Assert.Single(controller.ImportFormats);
        Assert.Equal(StagedSendMode.Simultaneous, import.StagedSendMode);
        Assert.Equal(TimeSpan.FromSeconds(2), import.StagedSendDelay);
    }

    private sealed class Dependency(string name)
    {
        public string Name { get; } = name;
    }

    private sealed class InjectedConfiguration(Dependency dependency, ILoggerFactory loggers) : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine)
            => new TestEngineConfiguration().Apply(engine)
                .CommandLineOverrides(loggers is not null && dependency.Name == "from-di");
    }

    /// <summary>The configuration is constructed through dependency injection: it receives the services the host registered and the logging services the engine always provides.</summary>
    [Fact]
    public async Task Build_Generic_InjectsServicesIntoTheConfiguration()
    {
        await using EngineBuilder builder = EngineBuilder.Build<InjectedConfiguration>(services => services.AddSingleton(new Dependency("from-di")));

        Assert.True(builder.AreCommandLineOverridesAllowed);
    }

    /// <summary>A configuration whose dependencies were not registered cannot be constructed, and fails with the container's own error.</summary>
    [Fact]
    public void Build_Generic_MissingDependency_Throws()
        => Assert.Throws<InvalidOperationException>(() => EngineBuilder.Build<InjectedConfiguration>(null));

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
        public void Configure(IEngineBuilder engine)
        {
            new TestEngineConfiguration().Apply(engine);
            GC.KeepAlive(service);
        }
    }

    /// <summary>The container the configuration was built in lives until the builder is disposed, since the configuration may have given the engine functions that use what was injected.</summary>
    [Fact]
    public async Task Build_Generic_KeepsTheContainerAliveUntilDisposed()
    {
        DisposableService service = new();
        EngineBuilder builder = EngineBuilder.Build<DisposingConfiguration>(services => services.AddSingleton(_ => service));

        await builder.DisposeAsync();

        Assert.True(service.IsDisposed);
    }

    /// <summary>Every server user stores messages, and no other user does.</summary>
    [Fact]
    public void StorageServers_AreAllTheServerUsers()
    {
        (_, EngineController controller) = Build(engine => engine, network: Network(
            ("Server1", new NetworkUserConfig { Role = "Server" }),
            ("Server2", new NetworkUserConfig { Role = "Server" }),
            ("Client1", new NetworkUserConfig { Role = "Client" })));

        Assert.Equal(["Server1", "Server2"], controller.StorageServers);
    }

    /// <summary>Levels take their order from how they are stated, their name from the member (or an override) and a neutral background and white foreground unless stated.</summary>
    [Fact]
    public void Levels_FollowTheStatedOrderWithOverrides()
    {
        (_, EngineController controller) = Build(engine => engine.Level(TestLevel.High).Label("TOP").Background("#222222").Foreground("#EEEEEE"));

        Assert.Equal(["PUBLIC", "TOP"], [controller.Levels[0].Label, controller.Levels[^1].Label]);
        Assert.Equal(["#5A5A5A", "#222222"], [controller.Levels[0].Background, controller.Levels[^1].Background]);
        Assert.Equal(["#FFFFFF", "#EEEEEE"], [controller.Levels[0].Foreground, controller.Levels[^1].Foreground]);
    }

    /// <summary>A user's level is the one on their entry, or the lowest configured level when none is stated.</summary>
    [Fact]
    public void GetUserLevel_UsesTheEntryElseTheLowestLevel()
    {
        (_, EngineController controller) = Build(
            engine => engine.Level(TestLevel.Low).Background("#111111").Level(TestLevel.High).Background("#222222"),
            network: Network(("ALICE", new NetworkUserConfig { LevelValue = TestLevel.High })));

        Assert.Equal(TestLevel.High, controller.GetUserLevel("ALICE"));
        Assert.Equal(TestLevel.Public, controller.GetUserLevel("BOB"));
    }

    /// <summary>Aspects are offered in the order stated, named by their member in uppercase unless a label is given, and without the call there are none.</summary>
    [Fact]
    public void Aspects_FollowTheStatedOrderWithLabels()
    {
        (_, EngineController none) = Build(engine => engine);
        (_, EngineController controller) = Build(engine => engine.Aspect(TestAspect.Signed).Label("SIGNED BY SENDER").Aspect(TestAspect.Encrypted));

        Assert.Empty(none.Aspects);
        Assert.Equal(["SIGNED BY SENDER", "ENCRYPTED"], controller.Aspects.Select(aspect => aspect.Label));
        Assert.Equal([1, 0], controller.Aspects.Select(aspect => aspect.Value));
    }

    /// <summary>An aspect is accepted when it was stated, none is accepted as none, and one that was never stated throws.</summary>
    [Fact]
    public void RequireAspect_StatedOnly()
    {
        (_, EngineController controller) = Build(engine => engine.Aspect(TestAspect.Signed).Label("SIGNED"));

        Assert.Equal(TestAspect.Signed, controller.RequireAspect(TestAspect.Signed));
        Assert.Null(controller.RequireAspect(null));
        Assert.Throws<ArgumentException>(() => controller.RequireAspect(TestAspect.Unused));
    }

    /// <summary>The aspect labels are the engine's own unless the display handler renames them, with the plural following the singular.</summary>
    [Fact]
    public void AspectLabels_DefaultAndFollowTheDisplayHandler()
    {
        (_, EngineController plain) = Build(engine => engine);
        (_, EngineController named) = Build(engine => engine.Display<TestDisplayHandler>());

        Assert.Equal(["Aspect", "Aspects"], [plain.AspectLabel, plain.AspectPluralLabel]);
        Assert.Equal(["Safeguard", "Safeguards"], [named.AspectLabel, named.AspectPluralLabel]);
    }
}
