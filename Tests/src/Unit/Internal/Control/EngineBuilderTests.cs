namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Control;

/// <summary>Unit tests for <see cref="EngineBuilder"/>: what a host states in its configuration is what <see cref="EngineController"/> reports, and anything left unstated takes the default.</summary>
public sealed class EngineBuilderTests
{
    private sealed class Configuration(Func<TestEngineBuilder, TestEngineBuilder> configure) : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine) => configure(new TestEngineConfiguration().Apply(engine));
    }

    private sealed class PacketConfiguration(Func<TestEngineBuilder, TestEngineBuilder> configure) : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine) => configure(new TestEngineConfiguration(packets: true).Apply(engine));
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

    private static (EngineBuilder Builder, EngineController Controller) BuildWith(Action<TestFrameBuilder>? message = null, Action<TestPacketBuilder>? packet = null, IServiceProvider? services = null)
    {
        EngineBuilder builder = packet is null ? EngineBuilder.Build(new TestEngineConfiguration(false, message)) : EngineBuilder.Build(new TestEngineConfiguration(false, message, packet));
        return (builder, new EngineController(builder, new CurrentUserProvider(), null, services));
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
        public void Configure(IEngineBuilder engine) => engine.Types<TestFrame, TestMessagePriority, TestLevel, TestAspect>().Priority(TestMessagePriority.Normal);
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
        builder.Types<TestFrame, TestMessagePriority, TestLevel, TestAspect>();

        Assert.Throws<InvalidOperationException>(() => builder.Types<TestFrame, TestMessagePriority, TestLevel, TestAspect>());
    }

    /// <summary>The no-priority and no-message-level types give a single NORMAL level and none, and packets cannot be stated without a packet type.</summary>
    [Fact]
    public void Types_NoPriorityAndNoMessageLevel_AndPacketsWithoutAPacketType()
    {
        EngineBuilder builder = new();
        IEngineBuilder<TestFrame, NoPacket, NoPriority, NoMessageLevel, NoMessageAspect> typed = builder.Types<TestFrame, NoPriority, NoMessageLevel, NoMessageAspect>();

        Assert.Equal(["NORMAL"], builder.PriorityOptions.Select(option => option.Name));
        Assert.Empty(builder.MessageLevelValues);
        Assert.Throws<InvalidOperationException>(() => typed.Packets<IPacketHandler<TestFrame, NoPacket>>(16 * 1024));
    }

    private sealed class UnorderedConfiguration : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine)
            => engine.Types<TestFrame, TestPacket, TestMessagePriority, TestLevel, TestAspect>()
                .Priority(TestMessagePriority.High).Priority(TestMessagePriority.Low).Priority(TestMessagePriority.Flash)
                .Level(TestLevel.Secret).Level(TestLevel.Public)
                .Frames<TestFrameHandler>()
                    .Heartbeat<TestHeartbeatHandler>();
    }

    private sealed class InterleavedConfiguration : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine)
            => engine.Types<TestFrame, TestPacket, TestMessagePriority, TestLevel, TestAspect>()
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
        Assert.Equal(["SECRET", "PUBLIC", "INTERNAL"], controller.MessageLevels.Select(level => level.Name));
    }

    /// <summary>Levels rank in the order they are stated, not the order of the enum, a member not stated is not a level, and what is stored is the member's integer value.</summary>
    [Fact]
    public void Levels_FollowTheStatedOrderAndStoreTheEnumValue()
    {
        EngineBuilder builder = EngineBuilder.Build(new UnorderedConfiguration());
        EngineController controller = new(builder, new CurrentUserProvider(), null);

        Assert.Equal([TestMessagePriority.High, TestMessagePriority.Low, TestMessagePriority.Flash], controller.Priorities.Select(level => level.Key).Take(3));
        Assert.Equal(3, controller.Priorities.Count);
        Assert.Equal(["SECRET", "PUBLIC"], controller.MessageLevels.Select(level => level.Name));
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
            => engine.Types<TestFrame, TestPacket, TestMessagePriority, TestLevel, TestAspect>()
                .Priority(TestMessagePriority.Normal).Label("SAME").Priority(TestMessagePriority.Flash).Label("same")
                .Frames<TestFrameHandler>();
    }

    /// <summary>Two priorities with the same name are refused, since the name is what users pick by.</summary>
    [Fact]
    public void Build_DuplicatePriorityNames_Throws()
        => Assert.Contains("SAME", Assert.Throws<InvalidOperationException>(() => EngineBuilder.Build(new DuplicateLabelConfiguration())).Message);

    /// <summary>A message level is named by its configured level, nothing for none, and anything else throws.</summary>
    [Fact]
    public void GetMessageLevelName_NullIsNone_UnconfiguredThrows()
    {
        (_, EngineController controller) = Build(engine => engine);

        Assert.Equal(["", "RESTRICTED"], [controller.GetMessageLevelName(null), controller.GetMessageLevelName(TestLevel.Restricted)]);
        Assert.Throws<ArgumentException>(() => controller.GetMessageLevelName(TestMessagePriority.High));
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
        builder.Types<TestFrame, TestMessagePriority, TestLevel, TestAspect>();

        Assert.Throws<InvalidOperationException>(() => EngineBuilder.Build(new NoPrioritiesConfiguration()));
    }

    private sealed class NoPrioritiesConfiguration : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine) => engine.Types<TestFrame, TestMessagePriority, TestLevel, TestAspect>().Frames<TestFrameHandler>();
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

    /// <summary>Everything a host can state about the application and the ports is reported back.</summary>
    [Fact]
    public void Stated_AppAndPortSettings_AreReported()
    {
        const string icon = "avares://Host/icon.png";
        (_, EngineController controller) = Build(engine => engine
            .Display<TestDisplayHandler>()
            .CommandLineOverrides(true));

        Assert.Equal("MyApp", controller.AppName);
        Assert.Equal("2.3.4", controller.AppVersion);
        Assert.True(controller.IsKioskMode);
        Assert.Equal("Welcome", controller.HomeText);
        Assert.Equal(LogFieldWidths.None, controller.LogWidths);
        Assert.Equal(["LINKED", "OFFLINE"], [controller.GetNetworkIndicatorLabel(true), controller.GetNetworkIndicatorLabel(false)]);
        Assert.Equal(["#2E7D32", "#112233"], [controller.GetNetworkIndicatorColor(true), controller.GetNetworkIndicatorColor(false)]);
        Assert.Equal(icon, controller.WindowIconPath);
        Assert.True(controller.CommandLineOverridesAllowed);
    }

    /// <summary>The data directory is the current user's own folder inside the application's folder under the application data root, and the application's folder itself before a user exists, where the install state always lives.</summary>
    [Fact]
    public void AppDataPath_IsTheCurrentUsersFolder()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        (_, EngineController noUser) = Build(engine => engine.Display<TestDisplayHandler>());
        (_, EngineController alice) = Build(engine => engine.Display<TestDisplayHandler>(), "ALICE");

        Assert.Equal(root, alice.AppDataRoot);
        Assert.Equal(Path.Combine(root, "MyApp"), noUser.AppDataPath);
        Assert.Equal(Path.Combine(root, "MyApp", "ALICE"), alice.AppDataPath);
        Assert.Equal(Path.Combine(root, "MyApp", "User.json"), alice.UserFilePath);
    }

    /// <summary>Without a draft handler every combination is allowed, and a stated handler is asked with the host's own enum members.</summary>
    [Fact]
    public void DraftHandler_DecidesWhichCombinationsAreAllowed()
    {
        Mock<IDraftHandler<TestMessagePriority, TestLevel, TestAspect>> handler = new() { CallBase = true };
        handler.Setup(b => b.IsAllowed(It.IsAny<IEngineContext>(), TestMessagePriority.High, TestLevel.Secret, TestAspect.Signed, "SPAM")).Returns(false);
        ServiceCollection services = new();
        services.AddSingleton(handler.Object);
        EngineBuilder builder = EngineBuilder.Build(new Configuration(engine => engine.Drafts<IDraftHandler<TestMessagePriority, TestLevel, TestAspect>>()));
        EngineController stated = new(builder, new CurrentUserProvider(), null, services.BuildServiceProvider());
        (_, EngineController plain) = Build(engine => engine);

        Assert.False(stated.IsDraftAllowed(Mock.Of<IEngineContext>(), TestMessagePriority.High, TestLevel.Secret, TestAspect.Signed, "SPAM"));
        Assert.True(stated.IsDraftAllowed(Mock.Of<IEngineContext>(), TestMessagePriority.High, null, null, "OK"));
        Assert.True(plain.IsDraftAllowed(Mock.Of<IEngineContext>(), TestMessagePriority.High, TestLevel.Secret, TestAspect.Signed, "SPAM"));
    }

    /// <summary>Alert, tag, priority and print settings a host states replace the defaults.</summary>
    [Fact]
    public void Stated_CompositionAndAlertSettings_AreReported()
    {
        (_, EngineController controller) = Build(engine => engine
            .Display<TestDisplayHandler>().Drafts<TestNoTagsDraftHandler>().Alarms<TestAlarmHandler>()
            .Priority(TestMessagePriority.High).Label("TOP").Mode(PriorityMode.System)
            .Prints<TestPrintHandler>()
            .Deletes<DraftsOnlyDeleteHandler>());

        Assert.Equal("ALARM", controller.AlertLabel);
        Assert.Equal(TimeSpan.FromSeconds(5), controller.AlarmSoundDuration);
        Assert.Equal(["NORMAL", "TOP"], [controller.Priorities[0].Name, controller.Priorities[^1].Name]);
        Assert.Equal([PriorityMode.User, PriorityMode.System], [controller.Priorities[0].Mode, controller.Priorities[^1].Mode]);
        Assert.False(controller.TagsEnabled);
        Assert.Equal("Category", controller.TagLabel);
        Assert.True(controller.PrintReceivedDefaultEnabled);
        Assert.True(controller.CanDelete(FolderType.Drafts));
        Assert.False(controller.CanDelete(FolderType.Inbox));
    }

    /// <summary>Without an info handler every name and word is the engine's own, and a handler renames what it states.</summary>
    [Fact]
    public void Display_StatedHandlerRenamesAndUnstatedKeepsDefaults()
    {
        (_, EngineController plain) = Build(engine => engine);
        (_, EngineController named) = Build(engine => engine.Display<TestDisplayHandler>());

        Assert.Equal(["HOME", "ALERT", "Tag", "Priority", "Message Level", "Inbox"], [plain.HomeText, plain.AlertLabel, plain.TagLabel, plain.PriorityLabel, plain.MessageLevelLabel, plain.Rename("Inbox")]);
        Assert.Equal(["Welcome", "ALARM", "Category", "Importance", "Classification", "Received", "Outbox"], [named.HomeText, named.AlertLabel, named.TagLabel, named.PriorityLabel, named.MessageLevelLabel, named.Rename("Inbox"), named.Rename("Outbox")]);
    }

    /// <summary>Fixed interface text written with the engine's own names shows what the host calls the concepts, in the case style of what it replaces, and is untouched when the host states nothing.</summary>
    [Fact]
    public void Display_ReplacesConceptNamesKeepingCaseStyle()
    {
        (_, EngineController plain) = Build(engine => engine);
        (_, EngineController named) = Build(engine => engine.Display<TestDisplayHandler>());

        Assert.Equal("NEW DRAFT in the Inbox, Alert only", plain.Display("NEW DRAFT in the Inbox, Alert only"));
        Assert.Equal("NEW DRAFT in the Received, ALARM only", named.Display("NEW DRAFT in the Inbox, Alert only"));
        Assert.Equal("Categories, importances and classifications, plus alarms.", named.Display("Tags, priorities and message levels, plus alerts."));
        Assert.Equal("Choose an importance, classification and category.", named.Display("Choose an priority, message level and tag."));
        Assert.Equal("No such user. Users are listed.", plain.Display("No such user. Users are listed."));
        Assert.Equal("No such operator. Operators are listed.", named.Display("No such user. Users are listed."));
        Assert.Equal("NO SUCH OPERATOR", named.Display("NO SUCH USER"));
    }

    /// <summary>A draft handler can turn tags off without affecting their label.</summary>
    [Fact]
    public void DraftHandler_EnableTagsFalse_TurnsTagsOff()
    {
        (_, EngineController controller) = Build(engine => engine.Display<TestDisplayHandler>().Drafts<TestNoTagsDraftHandler>());

        Assert.False(controller.TagsEnabled);
        Assert.Equal("Category", controller.TagLabel);
    }

    /// <summary>An overridden address type label replaces the default for that type only; the others keep theirs.</summary>
    [Fact]
    public void AddressTypes_Labelled_ReplacesOnlyThatTypesDefault()
    {
        (_, EngineController controller) = Build(engine => engine.AddressType(AddressType.External).Label("OUTSIDE"));

        Assert.Equal(["To", "Cc", "OUTSIDE"], controller.AddressTypes.Select(t => t.Label));
        Assert.Equal([AddressType.To, AddressType.Cc, AddressType.External], controller.AddressTypes.Select(t => t.Type));
    }

    /// <summary>The heartbeat intervals come from the heartbeat handler in use, the packet one first, and are 30 and 2 seconds when there is none.</summary>
    [Fact]
    public void HeartbeatIntervals_ComeFromTheHandler()
    {
        ServiceCollection services = new();
        services.AddSingleton(new TestHeartbeatHandler { Interval = TimeSpan.FromSeconds(5), RetryInterval = TimeSpan.FromSeconds(1) });
        EngineController frames = new(EngineBuilder.Build(new TestEngineConfiguration()), new CurrentUserProvider(), null, services.BuildServiceProvider());
        EngineController none = new(EngineBuilder.Build(new TestEngineConfiguration(heartbeats: false)), new CurrentUserProvider(), null);

        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1)], [frames.HeartbeatInterval, frames.HeartbeatRetryInterval]);
        Assert.Equal([TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)], [none.HeartbeatInterval, none.HeartbeatRetryInterval]);
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
    public void NetworkUsersGroupsAndData_AreReported()
    {
        (_, EngineController controller) = Build(engine => engine,
            network: new NetworkConfig
            {
                Users = { ["alice"] = new NetworkUserConfig { Data = new Dictionary<string, string> { ["desk"] = "4" } }, ["BOB"] = new NetworkUserConfig(), ["DAVE"] = new NetworkUserConfig() },
                UserGroups = { ["OPS"] = ["ALICE", "BOB", "DAVE"], ["EXTRA"] = ["DAVE"] }
            });

        Assert.Equal(["alice", "BOB", "DAVE", "OPS", "EXTRA"], controller.Users);
        Assert.Equal(["ALICE", "BOB", "DAVE"], controller.UserGroups["ops"]);
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

    /// <summary>A user name is found among the network's users, in the spelling the file uses, and nowhere else.</summary>
    [Fact]
    public void FindUserName_FindsNetworkUsersCaseInsensitively()
    {
        (_, EngineController none) = Build(engine => engine);
        (_, EngineController networked) = Build(engine => engine, network: Network(("ALICE", new NetworkUserConfig()), ("BOB", new NetworkUserConfig())));

        Assert.Null(none.FindUserName("CODE"));
        Assert.Equal("ALICE", networked.FindUserName("alice"));
        Assert.Equal("BOB", networked.FindUserName("Bob"));
        Assert.Null(networked.FindUserName("NOBODY"));
    }

    /// <summary>A user the network does not list is just a name; a listed user's details are what the file says.</summary>
    [Fact]
    public void GetUserInfo_ReturnsTheNetworksDetailsOrJustTheName()
    {
        (_, EngineController controller) = Build(engine => engine, network: Network(("ALICE", new NetworkUserConfig { MessageLevel = "HIGH" })));

        Assert.Equal("HIGH", controller.GetUserInfo("alice").MessageLevel);
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
        IEngineBuilder<TestFrame, TestPacket, TestMessagePriority, TestLevel, TestAspect> typed = builder.Types<TestFrame, TestPacket, TestMessagePriority, TestLevel, TestAspect>();

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

    /// <summary>A log handler fixes the widths of the fields it states, and without one no field is fixed.</summary>
    [Fact]
    public void LogHandler_FixesTheStatedWidths()
    {
        (_, EngineController none) = Build(engine => engine);
        (_, EngineController fixedWidths) = Build(engine => engine.Logs<TestLogHandler>());

        Assert.Equal(LogFieldWidths.None, none.LogWidths);
        Assert.Equal(new LogFieldWidths(8, 7, 3), fixedWidths.LogWidths);
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
        Mock<IEngineContext> engine = new();
        engine.Setup(e => e.CurrentUser).Returns(new UserInfo { Name = "ME" });
        engine.Setup(e => e.ConnectedUsers).Returns(new Dictionary<string, UserInfo> { ["BOB"] = new UserInfo { Name = "BOB" } });
        session.Setup(s => s.Engine).Returns(engine.Object);
        session.Setup(s => s.Connection).Returns(info);
        IPacketHandshakeContext<TestPacket>? seen = null;
        Mock<IPacketHandshakeHandler<TestPacket>> handler = new();
        handler.Setup(p => p.OnConnected(It.IsAny<IPacketHandshakeContext<TestPacket>>())).Callback((IPacketHandshakeContext<TestPacket> context) => seen = context);
        EngineBuilder builder = EngineBuilder.Build(new TestEngineConfiguration(false, null, packet => packet.Handshake<IPacketHandshakeHandler<TestPacket>>()));
        EngineController controller = new(builder, new CurrentUserProvider(), null, Services(handler.Object));
        TestPacket sent = new() { PayloadId = 5 };

        await controller.PacketHandshakeHandler!.OnConnected(session.Object);

        Assert.NotNull(seen);
        Assert.Equal("ME", seen.CurrentUser.Name);
        Assert.Contains("BOB", seen.ConnectedUsers.Keys);
        Assert.DoesNotContain("X", seen.ConnectedUsers.Keys);
        Assert.Same(info, seen.Connection);
        await seen.Send(sent);
        await seen.Connected("ALICE");
        await seen.Disconnect();
        session.Verify(s => s.Send(sent), Times.Once);
        session.Verify(s => s.Connected("ALICE"), Times.Once);
        session.Verify(s => s.Disconnect(), Times.Once);
    }

    /// <summary>Without a handshake there is none, and the identification hook leaves the decision to the engine.</summary>
    [Fact]
    public void Unstated_Identification_IsOff()
    {
        (_, EngineController controller) = Build(engine => engine);

        Assert.Null(controller.PacketHandshakeHandler);
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

        public Task OnConnected(INetworkConnectedContext<TestFrame, TestMessagePriority, TestLevel, TestAspect> context) => Task.CompletedTask;

        public Task OnDisconnected(INetworkDisconnectedContext<TestFrame, TestMessagePriority, TestLevel, TestAspect> context) => Task.CompletedTask;

        public Task OnReceived(INetworkReceivedContext<TestFrame, TestMessagePriority, TestLevel, TestAspect> context) => Task.CompletedTask;
    }

    private sealed class DependentHandlerConfiguration : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine)
            => engine.Types<TestFrame, TestPacket, TestMessagePriority, TestLevel, TestAspect>()
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

    private sealed class DraftsOnlyDeleteHandler : IDeleteHandler
    {
        public bool CanDelete(DeleteContext context) => context.Folder is FolderType.Drafts;
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

    private sealed class PlainConfiguration : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine) => new TestEngineConfiguration().Apply(engine);
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

    /// <summary>Every fluent call returns the builder, so a configuration can be one expression.</summary>
    [Fact]
    public void FluentCalls_ReturnTheBuilder()
    {
        TestEngineBuilder builder = new EngineBuilder().Types<TestFrame, TestPacket, TestMessagePriority, TestLevel, TestAspect>();

        Assert.Same(builder, builder.Display<TestDisplayHandler>().CommandLineOverrides(false));
    }

    /// <summary>With no servers in the network there are no storage servers.</summary>
    [Fact]
    public void StorageServers_Unstated_IsEmpty()
    {
        (_, EngineController controller) = Build(engine => engine);

        Assert.Empty(controller.StorageServers);
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

    /// <summary>Levels take their order from how they are stated, their name from the member (or an override) and a neutral color unless stated.</summary>
    [Fact]
    public void MessageLevels_FollowTheStatedOrderWithOverrides()
    {
        (_, EngineController controller) = Build(engine => engine.Level(TestLevel.High).Label("TOP").Color("#222222"));

        Assert.Equal(["PUBLIC", "TOP"], [controller.MessageLevels[0].Name, controller.MessageLevels[^1].Name]);
        Assert.Equal(["#5A5A5A", "#222222"], [controller.MessageLevels[0].Color, controller.MessageLevels[^1].Color]);
    }

    /// <summary>A user's message level is the one on their entry, or the lowest configured level when none is stated.</summary>
    [Fact]
    public void GetUserMessageLevel_UsesTheEntryElseTheLowestLevel()
    {
        (_, EngineController controller) = Build(
            engine => engine.Level(TestLevel.Low).Color("#111111").Level(TestLevel.High).Color("#222222"),
            network: Network(("ALICE", new NetworkUserConfig { MessageLevel = "HIGH" })));

        Assert.Equal("HIGH", controller.GetUserMessageLevel("ALICE"));
        Assert.Equal("PUBLIC", controller.GetUserMessageLevel("BOB"));
    }

    /// <summary>Aspects are offered in the order stated, named by their member in uppercase unless a label is given, and without the call there are none.</summary>
    [Fact]
    public void MessageAspects_FollowTheStatedOrderWithLabels()
    {
        (_, EngineController none) = Build(engine => engine);
        (_, EngineController controller) = Build(engine => engine.Aspect(TestAspect.Signed).Label("SIGNED BY SENDER").Aspect(TestAspect.Encrypted));

        Assert.Empty(none.MessageAspects);
        Assert.Equal(["SIGNED BY SENDER", "ENCRYPTED"], controller.MessageAspects.Select(aspect => aspect.Name));
        Assert.Equal([1, 0], controller.MessageAspects.Select(aspect => aspect.Value));
    }

    /// <summary>An aspect is named by its stated name, none is an empty name, and one that was never stated throws.</summary>
    [Fact]
    public void GetMessageAspectName_NamesStatedAspectsOnly()
    {
        (_, EngineController controller) = Build(engine => engine.Aspect(TestAspect.Signed).Label("SIGNED"));

        Assert.Equal("SIGNED", controller.GetMessageAspectName(TestAspect.Signed));
        Assert.Equal(string.Empty, controller.GetMessageAspectName(null));
        Assert.Throws<ArgumentException>(() => controller.GetMessageAspectName(TestAspect.Unused));
    }

    /// <summary>Two aspects cannot share a name.</summary>
    [Fact]
    public void MessageAspects_DuplicateNames_Throw()
        => Assert.Throws<InvalidOperationException>(() => Build(engine => engine.Aspect(TestAspect.Signed).Label("X").Aspect(TestAspect.Encrypted).Label("x")));

    /// <summary>The aspect labels are the engine's own unless the display handler renames them, with the plural following the singular.</summary>
    [Fact]
    public void MessageAspectLabels_DefaultAndFollowTheDisplayHandler()
    {
        (_, EngineController plain) = Build(engine => engine);
        (_, EngineController named) = Build(engine => engine.Display<TestDisplayHandler>());

        Assert.Equal(["Message Aspect", "Message Aspects"], [plain.MessageAspectLabel, plain.MessageAspectPluralLabel]);
        Assert.Equal(["Safeguard", "Safeguards"], [named.MessageAspectLabel, named.MessageAspectPluralLabel]);
    }
}
