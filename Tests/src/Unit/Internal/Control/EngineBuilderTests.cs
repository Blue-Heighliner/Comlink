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

    private static IServiceProvider Services(params object[] processors)
    {
        ServiceCollection services = new();
        foreach (object processor in processors)
        {
            foreach (Type type in processor.GetType().GetInterfaces().Where(type => type.IsGenericType && type.Namespace == typeof(IInitialFrameProcessor<>).Namespace && type.Name.EndsWith("Processor`1"))) { services.AddSingleton(type, processor); }
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
        public void Configure(IEngineBuilder engine) => engine.Types<TestFrame, TestMessagePriority, TestLevel>().Priorities().Priority(TestMessagePriority.Normal);
    }

    /// <summary>A configuration that states its types but no frame handlers cannot start the engine.</summary>
    [Fact]
    public void Build_TypesWithoutFrames_Throws()
        => Assert.Contains("Frames(", Assert.Throws<InvalidOperationException>(() => EngineBuilder.Build(new TypesOnlyConfiguration())).Message);

    /// <summary>The types can only be stated once.</summary>
    [Fact]
    public void Types_StatedTwice_Throws()
    {
        EngineBuilder builder = new();
        builder.Types<TestFrame, TestMessagePriority, TestLevel>();

        Assert.Throws<InvalidOperationException>(() => builder.Types<TestFrame, TestMessagePriority, TestLevel>());
    }

    /// <summary>The no-priority and no-security-level types give a single NORMAL level and none, and packets cannot be stated without a packet type.</summary>
    [Fact]
    public void Types_NoPriorityAndNoSecurityLevel_AndPacketsWithoutAPacketType()
    {
        EngineBuilder builder = new();
        IEngineBuilder<TestFrame, NoPacket, NoPriority, NoSecurityLevel> typed = builder.Types<TestFrame, NoPriority, NoSecurityLevel>();

        Assert.Equal(["NORMAL"], builder.PriorityOptions.Select(option => option.Name));
        Assert.Empty(builder.SecurityLevelValues);
        Assert.Throws<InvalidOperationException>(() => typed.Packets());
    }

    private sealed class UnorderedConfiguration : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine)
            => engine.Types<TestFrame, TestPacket, TestMessagePriority, TestLevel>()
                .Priorities().Priority(TestMessagePriority.High).Priority(TestMessagePriority.Low).Priority(TestMessagePriority.Flash)
                .SecurityLevels().Level(TestLevel.Secret).Level(TestLevel.Public)
                .Frames()
                    .Message<TestMessageHandler>()
                    .Retrieval<TestRetrievalHandler>()
                    .ReadReceipt<TestReadReceiptHandler>()
                    .ReceiveReceipt<TestReceiveReceiptHandler>();
    }

    /// <summary>Levels rank in the order they are stated, not the order of the enum, a member not stated is not a level, and what is stored is the member's integer value.</summary>
    [Fact]
    public void Levels_FollowTheStatedOrderAndStoreTheEnumValue()
    {
        EngineBuilder builder = EngineBuilder.Build(new UnorderedConfiguration());
        EngineController controller = new(builder, new CurrentUserProvider(), null);

        Assert.Equal([TestMessagePriority.High, TestMessagePriority.Low, TestMessagePriority.Flash], controller.Priorities.Select(level => level.Key).Take(3));
        Assert.Equal(3, controller.Priorities.Count);
        Assert.Equal(["SECRET", "PUBLIC"], controller.SecurityLevels.Select(level => level.Name));
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

    /// <summary>A received message with an unconfigured priority or security level is invalid, with the reason, so it is dropped and logged.</summary>
    [Fact]
    public void GetInvalidMessageReason_UnconfiguredPriorityOrSecurityLevel_IsReported()
    {
        EngineController controller = new(EngineBuilder.Build(new UnorderedConfiguration()), new CurrentUserProvider(), null);

        Assert.Null(controller.GetInvalidMessageReason(new TestFrame { MessageId = "M", Priority = "FLASH", SecurityLevel = "SECRET" }));
        Assert.Null(controller.GetInvalidMessageReason(new TestFrame { MessageId = "M", Priority = "FLASH" }));
        Assert.Contains("priority", controller.GetInvalidMessageReason(new TestFrame { MessageId = "M", Priority = "LEVEL3" }));
        Assert.Contains("security level", controller.GetInvalidMessageReason(new TestFrame { MessageId = "M", Priority = "FLASH", SecurityLevel = "LOW" }));
        Assert.Contains("identifier", controller.GetInvalidMessageReason(new TestFrame { Priority = "FLASH" }));
    }

    private sealed class DuplicateLabelConfiguration : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine)
            => engine.Types<TestFrame, TestPacket, TestMessagePriority, TestLevel>()
                .Priorities().Priority(TestMessagePriority.Normal).Label("SAME").Priority(TestMessagePriority.Flash).Label("same")
                .Frames().Message<TestMessageHandler>().Retrieval<TestRetrievalHandler>().ReadReceipt<TestReadReceiptHandler>().ReceiveReceipt<TestReceiveReceiptHandler>();
    }

    /// <summary>Two priorities with the same name are refused, since the name is what users pick by.</summary>
    [Fact]
    public void Build_DuplicatePriorityNames_Throws()
        => Assert.Contains("SAME", Assert.Throws<InvalidOperationException>(() => EngineBuilder.Build(new DuplicateLabelConfiguration())).Message);

    /// <summary>Stating a frame handler before Frames() says so, rather than failing with a null reference.</summary>
    [Fact]
    public void FrameHandlers_BeforeFrames_ThrowAClearError()
    {
        EngineBuilder state = new();
        TestEngineBuilder typed = state.Types<TestFrame, TestPacket, TestMessagePriority, TestLevel>();

        Assert.Contains("Frames()", Assert.Throws<InvalidOperationException>(() => ((IFrameBuilder<TestFrame, TestPacket, TestMessagePriority, TestLevel>)typed).Message<TestMessageHandler>()).Message);
    }

    /// <summary>A receipt or retrieval with no destination goes nowhere rather than to an empty user name.</summary>
    [Fact]
    public void Route_EmptyDestination_GoesNowhere()
    {
        (_, EngineController controller) = Build(engine => engine);

        Assert.Empty(controller.Route(new TestFrame { IsHidden = true, ReadReceiptMessageId = "M", Addresses = [new TestAddressEntry { UserName = "" }] }));
        Assert.Equal(["ALICE"], controller.Route(new TestFrame { IsHidden = true, ReadReceiptMessageId = "M", Addresses = [new TestAddressEntry { UserName = "ALICE" }] }));
    }

    /// <summary>A security level is named by its configured level, nothing for none, and anything else throws.</summary>
    [Fact]
    public void GetSecurityLevelName_NullIsNone_UnconfiguredThrows()
    {
        (_, EngineController controller) = Build(engine => engine);

        Assert.Equal(["", "RESTRICTED"], [controller.GetSecurityLevelName(null), controller.GetSecurityLevelName(TestLevel.Restricted)]);
        Assert.Throws<ArgumentException>(() => controller.GetSecurityLevelName(TestMessagePriority.High));
    }

    /// <summary>Without a draft handler the draft view offers no width and no header, and the handler's range, initial width and header are reported otherwise.</summary>
    [Fact]
    public void Drafts_HandlerStatesTheWidthRangeAndHeader()
    {
        (_, EngineController none) = Build(engine => engine);
        (_, EngineController full) = Build(engine => engine.Drafts<TestDraftHandler>());
        (_, EngineController headerOnly) = Build(engine => engine.Drafts<TestHeaderOnlyDraftHandler>());
        (_, EngineController maxOnly) = Build(engine => engine.Drafts<TestMaxOnlyDraftHandler>());
        DraftContent withTag = new() { Tag = "X", Priority = TestMessagePriority.Normal, SecurityLevel = "", IsAlert = false, Addresses = [], LineWidth = null };

        Assert.Null(none.DraftLineWidth);
        Assert.Null(none.GetDraftHeader(withTag));
        Assert.Equal(new LineWidthRange(60, 20, 80), full.DraftLineWidth);
        Assert.Equal((60, 20, 80), (full.DraftLineWidth!.Initial, full.DraftLineWidth.Clamp(5), full.DraftLineWidth.Clamp(500)));
        Assert.Equal("TAG: X", full.GetDraftHeader(withTag));
        Assert.Null(full.GetDraftHeader(withTag with { Tag = "" }));
        Assert.Null(headerOnly.DraftLineWidth);
        Assert.Equal("HEADER", headerOnly.GetDraftHeader(withTag));
        Assert.Equal(40, maxOnly.DraftLineWidth!.Initial);
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
        Assert.Equal(new DraftDefaults("NEWTAG", TestMessagePriority.Level3, TestLevel.Restricted), ruled.DraftDefaults);
    }

    /// <summary>A priority enum other than the no-priority one needs its levels stated.</summary>
    [Fact]
    public void Build_WithoutPriorityLevels_Throws()
    {
        EngineBuilder builder = new();
        builder.Types<TestFrame, TestMessagePriority, TestLevel>();

        Assert.Throws<InvalidOperationException>(() => EngineBuilder.Build(new NoPrioritiesConfiguration()));
    }

    private sealed class NoPrioritiesConfiguration : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine) => engine.Types<TestFrame, TestMessagePriority, TestLevel>().Frames();
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
        Assert.Equal(["Space", "Enter"], controller.AlertQuickReadKeys);
        Assert.Equal("Tag", controller.TagLabel);
        Assert.True(controller.TagsEnabled);
        Assert.False(controller.PrintReceivedDefaultEnabled);
        Assert.Equal(UserRole.Client, controller.Role);
        Assert.False(controller.CommandLineOverridesAllowed);
        Assert.Empty(controller.OutgoingPoints);
        Assert.Empty(controller.Servers);
        Assert.Empty(controller.ExternalSystems);
        Assert.Null(controller.NetworkHandler);
        Assert.Null(controller.PacketType);
        Assert.Equal(Enum.GetValues<TestMessagePriority>().Length, controller.Priorities.Count);
        Assert.Equal("NORMAL", controller.Priorities[0].Name);
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
            .Display<TestDisplayHandler>()
            .CommandLineOverrides(true));

        Assert.Equal("MyApp", controller.AppName);
        Assert.Equal("2.3.4", controller.AppVersion);
        Assert.True(controller.IsKioskMode);
        Assert.Equal("Welcome", controller.HomeText);
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
        Assert.Equal(Path.Combine(root, "MyApp", "State.json"), alice.StatePath);
    }

    /// <summary>Alert, tag, priority and print settings a host states replace the defaults.</summary>
    [Fact]
    public void Stated_CompositionAndAlertSettings_AreReported()
    {
        (_, EngineController controller) = Build(engine => engine
            .Display<TestDisplayHandler>().Drafts<TestNoTagsDraftHandler>().Alarms<TestAlarmHandler>()
            .Priorities().Priority(TestMessagePriority.High).Label("TOP").Mode(PriorityMode.System).Block(null, "SPAM").Block(TestMessagePriority.High, null)
            .Prints<TestPrintHandler>()
            .Deletes<DraftsOnlyDeleteHandler>());

        Assert.Equal("ALARM", controller.AlertLabel);
        Assert.Equal(TimeSpan.FromSeconds(5), controller.AlarmSoundDuration);
        Assert.Equal(["NORMAL", "TOP"], [controller.Priorities[0].Name, controller.Priorities[^1].Name]);
        Assert.Equal([PriorityMode.User, PriorityMode.System], [controller.Priorities[0].Mode, controller.Priorities[^1].Mode]);
        Assert.False(controller.TagsEnabled);
        Assert.Equal("Category", controller.TagLabel);
        Assert.Equal(2, controller.BlockedCombinations.Count);
        Assert.Equal(TestMessagePriority.High, controller.BlockedCombinations[1].Priority);
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

        Assert.Equal(["HOME", "ALERT", "Tag", "Priority", "Security Level", "Inbox"], [plain.HomeText, plain.AlertLabel, plain.TagLabel, plain.PriorityLabel, plain.SecurityLevelLabel, plain.Rename("Inbox")]);
        Assert.Equal(["Welcome", "ALARM", "Category", "Importance", "Classification", "Received", "Outbox"], [named.HomeText, named.AlertLabel, named.TagLabel, named.PriorityLabel, named.SecurityLevelLabel, named.Rename("Inbox"), named.Rename("Outbox")]);
    }

    /// <summary>Fixed interface text written with the engine's own names shows what the host calls the concepts, in the case style of what it replaces, and is untouched when the host states nothing.</summary>
    [Fact]
    public void Display_ReplacesConceptNamesKeepingCaseStyle()
    {
        (_, EngineController plain) = Build(engine => engine);
        (_, EngineController named) = Build(engine => engine.Display<TestDisplayHandler>());

        Assert.Equal("NEW DRAFT in the Inbox, Alert only", plain.Display("NEW DRAFT in the Inbox, Alert only"));
        Assert.Equal("NEW DRAFT in the Received, ALARM only", named.Display("NEW DRAFT in the Inbox, Alert only"));
        Assert.Equal("Categories, importances and classifications, plus alarms.", named.Display("Tags, priorities and security levels, plus alerts."));
        Assert.Equal("Choose an importance, classification and category.", named.Display("Choose an priority, security level and tag."));
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
        (_, EngineController controller) = Build(engine => engine.AddressTypes().Type(AddressType.External).Label("OUTSIDE"));

        Assert.Equal(["To", "Cc", "OUTSIDE"], controller.AddressTypes.Select(t => t.Label));
        Assert.Equal([AddressType.To, AddressType.Cc, AddressType.External], controller.AddressTypes.Select(t => t.Type));
    }

    /// <summary>A retrieval request, read receipt and receive receipt are sent with the priority their handler names, and a message with its own.</summary>
    [Fact]
    public void GetPriority_FollowsTheKindOfFrame()
    {
        ServiceCollection services = new();
        services.AddSingleton(new TestRetrievalHandler { Priority = TestMessagePriority.Level1 });
        services.AddSingleton(new TestReadReceiptHandler { Priority = TestMessagePriority.Receipt });
        services.AddSingleton(new TestReceiveReceiptHandler { Priority = TestMessagePriority.Receipt });
        EngineBuilder builder = EngineBuilder.Build(new Configuration(engine => engine.Priorities().Priority(TestMessagePriority.Level1).Mode(PriorityMode.System).Priority(TestMessagePriority.Receipt).Mode(PriorityMode.System)));
        EngineController controller = new(builder, new CurrentUserProvider(), null, services.BuildServiceProvider());

        Assert.Equal(1, controller.GetPriority(new TestFrame { IsHidden = true, IsRetrieval = true }));
        Assert.Equal(12, controller.GetPriority(new TestFrame { IsHidden = true, ReadReceiptMessageId = "M" }));
        Assert.Equal(12, controller.GetPriority(new TestFrame { IsHidden = true, ReceiveReceiptMessageId = "M" }));
        Assert.Equal(0, controller.GetPriority(new TestFrame()));
    }

    /// <summary>A stated heartbeat handler creates, recognizes and prioritizes heartbeats; without one there are none.</summary>
    [Fact]
    public void Heartbeat_IsOptionalAndHandlerDriven()
    {
        EngineController withHandler = new(EngineBuilder.Build(new Configuration(engine => engine)), new CurrentUserProvider(), null);
        EngineController without = new(EngineBuilder.Build(new TestEngineConfiguration(heartbeats: false)), new CurrentUserProvider(), null);

        object heartbeat = withHandler.CreateHeartbeat();
        Assert.True(withHandler.HeartbeatsEnabled);
        Assert.True(withHandler.IsHeartbeat(heartbeat));
        Assert.False(withHandler.IsHeartbeat(new TestFrame()));
        Assert.Equal(0, withHandler.GetPriority(heartbeat));
        Assert.False(without.HeartbeatsEnabled);
        Assert.False(without.IsHeartbeat(heartbeat));
        Assert.Throws<InvalidOperationException>(() => without.CreateHeartbeat());
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

    /// <summary>The next message id comes from the message handler, and is a 32 character uppercase GUID when it does not say.</summary>
    [Fact]
    public void NextId_ComesFromTheMessageHandler()
    {
        ServiceCollection services = new();
        services.AddSingleton(new TestMessageHandler { Ids = previous => $"{previous ?? "0"}1" });
        EngineController stated = new(EngineBuilder.Build(new TestEngineConfiguration()), new CurrentUserProvider(), null, services.BuildServiceProvider());
        (_, EngineController unstated) = Build(engine => engine);

        Assert.Equal("01", stated.NextId(null));
        Assert.Equal("A1", stated.NextId("A"));
        string guid = unstated.NextId(null);
        Assert.Equal(32, guid.Length);
        Assert.Equal(guid.ToUpperInvariant(), guid);
        Assert.NotEqual(guid, unstated.NextId(guid));
    }

    /// <summary>The sender is read and written through the handler of the frame's kind, and a frame of no kind has none.</summary>
    [Fact]
    public void Sender_GoesThroughTheHandlerOfTheFramesKind()
    {
        (_, EngineController controller) = Build(engine => engine);
        TestFrame message = new();
        TestFrame retrieval = new() { IsHidden = true, IsRetrieval = true };
        TestFrame receipt = new() { IsHidden = true, ReadReceiptMessageId = "M" };
        TestFrame heartbeat = new() { IsHidden = true, IsHeartbeat = true };

        foreach (TestFrame frame in new[] { message, retrieval, receipt, heartbeat }) { controller.SetFromUser(frame, "ALICE"); }

        Assert.Equal(["ALICE", "ALICE", "ALICE", string.Empty], [controller.GetFromUser(message), controller.GetFromUser(retrieval), controller.GetFromUser(receipt), controller.GetFromUser(heartbeat)]);
        Assert.Equal(string.Empty, heartbeat.FromUser);
    }

    /// <summary>Only messages have an identifier: a receipt is identified by the message it is for, and any other frame has none.</summary>
    [Fact]
    public void GetIdentifier_IsTheMessageIdOrTheReceiptedMessageId()
    {
        (_, EngineController controller) = Build(engine => engine);

        Assert.Equal("M1", controller.GetIdentifier(new TestFrame { MessageId = "M1" }));
        Assert.Equal("M2", controller.GetIdentifier(new TestFrame { IsHidden = true, ReadReceiptMessageId = "M2" }));
        Assert.Equal("M3", controller.GetIdentifier(new TestFrame { IsHidden = true, ReceiveReceiptMessageId = "M3" }));
        Assert.Equal(string.Empty, controller.GetIdentifier(new TestFrame { IsHidden = true, IsHeartbeat = true }));
    }

    /// <summary>With no priorities configured everything goes at priority 0, and with some configured nothing goes outside them: a name that is empty or unknown, or a level that is not configured, is the lowest level.</summary>
    [Fact]
    public void Priorities_ResolveWithinTheConfiguredLevels()
    {
        (_, EngineController configured) = Build(engine => engine);

        Assert.Equal([TestMessagePriority.Normal, TestMessagePriority.Receipt, TestMessagePriority.Normal, TestMessagePriority.Normal], [configured.PriorityOf(null), configured.PriorityOf((int)TestMessagePriority.Receipt), configured.PriorityOf(999), configured.PriorityOf(0)]);
        Assert.Equal([TestMessagePriority.Normal, TestMessagePriority.Flash], [configured.ResolvePriority(TestLevel.High), configured.ResolvePriority(TestMessagePriority.Flash)]);
        Assert.Equal(15, configured.HighestPriority);
        Assert.Equal(12, configured.GetPriority(new TestFrame { Priority = "receipt" }));
        Assert.Equal(0, configured.GetPriority(new TestFrame { Priority = "BOGUS" }));
        Assert.Equal(TestMessagePriority.Receipt, configured.GetMessagePriority(new TestFrame { Priority = "Receipt" }));
        Assert.Equal("NORMAL", ((TestFrame)configured.CreateMessage(new MessageContent { SentAt = DateTime.UtcNow, Body = "B", Priority = TestMessagePriority.Normal, Tag = "", SecurityLevel = "" })).Priority);
        Assert.Throws<ArgumentException>(() => configured.CreateMessage(new MessageContent { SentAt = DateTime.UtcNow, Body = "B", Priority = TestLevel.High, Tag = "", SecurityLevel = "" }));
        Assert.Throws<ArgumentException>(() => configured.CreateMessage(new MessageContent { SentAt = DateTime.UtcNow, Body = "B", Priority = TestMessagePriority.Normal, Tag = "", SecurityLevel = "BOGUS" }));
    }

    /// <summary>The packet size and window are stated on the packet configuration, defaulting to 16 KiB and 1.</summary>
    [Fact]
    public void PacketSizeAndWindow_AreStatedOnPackets()
    {
        (_, EngineController stated) = Build(engine => engine.Packets().Frame<TestFramePacketHandler>().Size(1024).Window(3));
        (_, EngineController unstated) = Build(engine => engine);

        Assert.Equal((1024, 3), (stated.PacketSize, stated.PacketWindow));
        Assert.Equal((16 * 1024, 1), (unstated.PacketSize, unstated.PacketWindow));
    }

    /// <summary>Stating priorities twice replaces the earlier list rather than adding to it.</summary>
    [Fact]
    public void Priorities_StatedTwice_ReplacesTheEarlierList()
    {
        (_, EngineController controller) = Build(engine => engine
            .Priorities().Priority(TestMessagePriority.Flash).Label("FIRST")
            .Priorities().Priority(TestMessagePriority.Flash).Label("SECOND"));

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

    /// <summary>Installation codes resolve to a user name through the install handler; by default a code is the name of a user of the network, and otherwise only the code CODE is recognized.</summary>
    [Fact]
    public void InstallHandler_ReplacesTheDefault()
    {
        (_, EngineController stated) = Build(engine => engine.Installs<TestInstallHandler>());
        (_, EngineController fallback) = Build(engine => engine);

        Assert.Equal("XUSER", stated.ResolveUserName("X"));
        Assert.Null(stated.ResolveUserName("CODE"));
        Assert.Equal("TEST", fallback.ResolveUserName("code"));
        Assert.Null(fallback.ResolveUserName("X"));

        (_, EngineController networked) = Build(engine => engine, network: Network(("ALICE", new NetworkUserConfig()), ("BOB", new NetworkUserConfig())));
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

    /// <summary>The stated MSMT options are used with the engine's own credentials, and the built options are used as they are when none are stated.</summary>
    [Fact]
    public void Stated_MsmtOptions_ReplaceTheSettingsUsedForEveryConnectionKeepingTheEnginesCredentials()
    {
        MsmtSessionPeerOptions options = new() { Credentials = new MsmtCredentials { Identity = TestMsmtCertificates.Create().Server, TrustedAuthorities = [] } };
        (_, EngineController adjusted) = Build(engine => engine
            .Connections().Msmt(new MsmtConnectionOptions { HandshakeTimeout = TimeSpan.FromSeconds(7) }));
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
            .Connections().Hdlc(new HdlcPeerOptions { MaxInfoField = 512, Link = new HdlcPeerOptions().Link with { Crc = HdlcCrc.Crc32Ccitt } }));
        (_, EngineController plain) = Build(engine => engine);

        Assert.Equal(512, adjusted.HdlcOptions.MaxInfoField);
        Assert.Equal(HdlcCrc.Crc32Ccitt, adjusted.HdlcOptions.Link.Crc);
        Assert.Equal(new HdlcPeerOptions(), plain.HdlcOptions);
    }

    /// <summary>The print count comes from the print handler.</summary>
    [Fact]
    public void PrintCount_ComesFromPrintHandler()
    {
        (_, EngineController controller) = Build(engine => engine.Prints<TestPrintHandler>());

        Assert.Equal(2, controller.GetPrintCount(new TestFrame { PrintCount = 2 }));
        Assert.Equal(1, controller.GetPrintCount(new TestFrame()));
    }

    /// <summary>The initial message and packet processors are reported and used.</summary>
    [Fact]
    public void Stated_Identification_IsUsed()
    {
        Mock<IInitialFrameProcessor<TestFrame>> messages = new();
        Mock<IInitialPacketProcessor<TestPacket>> packets = new();
        EngineBuilder builder = EngineBuilder.Build(new TestEngineConfiguration(
            false,
            message => message.InitialProcessor<IInitialFrameProcessor<TestFrame>>(),
            packet => packet.InitialProcessor<IInitialPacketProcessor<TestPacket>>()));
        EngineController controller = new(builder, new CurrentUserProvider(), null, Services(messages.Object, packets.Object));
        Mock<IInitialSession> session = new();
        TestFrame initialMessage = new();
        TestPacket initialPacket = new();

        controller.InitialFrameProcessor!.OnConnected(session.Object);
        controller.InitialFrameProcessor.OnReceived(session.Object, initialMessage);
        controller.InitialPacketProcessor!.OnReceived(session.Object, initialPacket);

        Assert.Equal(typeof(TestFrame), controller.InitialFrameProcessor.ItemType);
        Assert.Equal(typeof(TestPacket), controller.InitialPacketProcessor.ItemType);
        messages.Verify(m => m.OnConnected(It.IsAny<IInitialFrameContext<TestFrame>>()), Times.Once);
        messages.Verify(m => m.OnReceived(It.IsAny<IInitialFrameContext<TestFrame>>(), initialMessage), Times.Once);
        packets.Verify(p => p.OnReceived(It.IsAny<IInitialPacketContext<TestPacket>>(), initialPacket), Times.Once);
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
        session.Setup(s => s.Connection).Returns(info);
        IInitialFrameContext<TestFrame>? seen = null;
        Mock<IInitialFrameProcessor<TestFrame>> processor = new();
        processor.Setup(p => p.OnConnected(It.IsAny<IInitialFrameContext<TestFrame>>())).Callback((IInitialFrameContext<TestFrame> context) => seen = context);
        EngineBuilder builder = EngineBuilder.Build(new TestEngineConfiguration(false, message => message.InitialProcessor<IInitialFrameProcessor<TestFrame>>()));
        EngineController controller = new(builder, new CurrentUserProvider(), null, Services(processor.Object));
        TestFrame sent = new() { Body = "HI" };

        controller.InitialFrameProcessor!.OnConnected(session.Object);

        Assert.NotNull(seen);
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

        Assert.Null(controller.InitialPacketProcessor);
        Assert.Null(controller.InitialFrameProcessor);
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
        (_, EngineController controller) = Build(engine => engine.Exports().Format<FirstExportFormat>().Format<ReplacingExportFormat>().Imports().Format<SlowImportFormat>());

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

    private sealed class DraftsOnlyDeleteHandler : IDeleteHandler
    {
        public bool CanDelete(DeleteContext context) => context.Folder == FolderType.Drafts;
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
            new TestEngineConfiguration().Apply(engine).Installs<TestInstallHandler>();
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
        TestEngineBuilder builder = new EngineBuilder().Types<TestFrame, TestPacket, TestMessagePriority, TestLevel>();

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
    public void SecurityLevels_FollowTheStatedOrderWithOverrides()
    {
        (_, EngineController controller) = Build(engine => engine.SecurityLevels().Level(TestLevel.High).Label("TOP").Color("#222222"));

        Assert.Equal(["PUBLIC", "TOP"], [controller.SecurityLevels[0].Name, controller.SecurityLevels[^1].Name]);
        Assert.Equal(["#5A5A5A", "#222222"], [controller.SecurityLevels[0].Color, controller.SecurityLevels[^1].Color]);
    }

    /// <summary>A user's security level is the one on their entry, or the lowest configured level when none is stated.</summary>
    [Fact]
    public void GetUserSecurityLevel_UsesTheEntryElseTheLowestLevel()
    {
        (_, EngineController controller) = Build(
            engine => engine.SecurityLevels().Level(TestLevel.Low).Color("#111111").Level(TestLevel.High).Color("#222222"),
            network: Network(("ALICE", new NetworkUserConfig { SecurityLevel = "HIGH" })));

        Assert.Equal("HIGH", controller.GetUserSecurityLevel("ALICE"));
        Assert.Equal("PUBLIC", controller.GetUserSecurityLevel("BOB"));
    }
}
