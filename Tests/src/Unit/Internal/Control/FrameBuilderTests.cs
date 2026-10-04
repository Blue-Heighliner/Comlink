namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Control;

/// <summary>Unit tests for <see cref="FrameBuilder{TFrame, TPriority, TLevel}"/> and the <see cref="FrameMap"/> it produces.</summary>
public sealed class FrameBuilderTests
{
    private static EngineBuilder Types()
    {
        EngineBuilder state = new();
        state.Types<TestFrame, TestMessagePriority, TestLevel>();
        SecurityLevelBuilder<TestLevel> levels = new();
        foreach (TestLevel level in Enum.GetValues<TestLevel>()) { levels.Level(level); }

        state.SecurityLevelValues.AddRange(levels.Build());
        return state;
    }

    private static FrameBuilder<TestFrame, TestMessagePriority, TestLevel> Complete()
    {
        FrameBuilder<TestFrame, TestMessagePriority, TestLevel> builder = new(Types());
        builder
            .Message<TestMessageHandler>()
            .Retrieval<TestRetrievalHandler>()
            .ReadReceipt<TestReadReceiptHandler>()
            .ReceiveReceipt<TestReceiveReceiptHandler>();
        return builder;
    }

    /// <summary>Building fails and names every logical field and frame kind that was not stated.</summary>
    [Fact]
    public void Build_UnstatedFields_ThrowsNamingThem()
    {
        FrameBuilder<TestFrame, TestMessagePriority, TestLevel> builder = new(new EngineBuilder());
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("TestFrame", error.Message);
        string[] missing = error.Message[(error.Message.IndexOf(':') + 1)..].Split(',', StringSplitOptions.TrimEntries);
        Assert.Equal(["Message", "Retrieval", "ReadReceipt", "ReceiveReceipt"], missing);
    }

    /// <summary>The map creates the host's frame and reports its type.</summary>
    [Fact]
    public void Map_CreatesTheHostsFrame()
    {
        FrameMap map = Complete().Build();

        Assert.IsType<TestFrame>(map.Create());
        Assert.Equal(typeof(TestFrame), map.Type);
    }

    /// <summary>The message handler the host states creates a message from its content, recognizes it, and reads its content back.</summary>
    [Fact]
    public void MessageHandler_CreatesRecognizesAndReads()
    {
        IMessageFrameHandler handler = Complete().Build().Message.Create(null);
        DateTime sentAt = new(2026, 9, 28, 1, 2, 3, DateTimeKind.Utc);

        object message = handler.Create(new MessageContent { SentAt = sentAt, Body = "BODY", IsAlert = true, Priority = TestMessagePriority.Level7, Tag = "TAG", SecurityLevel = "SECRET" });

        Assert.IsType<TestFrame>(message);
        Assert.True(handler.IsValid(message));
        Assert.False(handler.IsValid(new TestFrame { IsHidden = true }));
        Assert.Equal((sentAt, "BODY", true, TestMessagePriority.Level7, "TAG", "SECRET"), (handler.GetSentAt(message), handler.GetBody(message), handler.GetIsAlert(message), handler.GetPriority(message), handler.GetTag(message), handler.GetSecurityLevel(message)));
    }

    /// <summary>The retrieval handler the host states creates a request from its criteria, recognizes it, and reads the criteria back.</summary>
    [Fact]
    public void RetrievalHandler_CreatesRecognizesAndReads()
    {
        IRetrievalFrameHandler handler = Complete().Build().Retrieval.Create(null);
        DateTime from = new(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);

        object request = handler.Create(new RetrievalCreateContext { Server = "SERVER", From = from, Authors = ["ALICE"], Destinations = ["BOB"], Ids = ["M1"] });

        Assert.True(handler.IsValid(request));
        Assert.False(handler.IsValid(new TestFrame()));
        Assert.Equal(from, handler.GetFrom(request));
        Assert.Null(handler.GetTo(request));
        Assert.Equal(["ALICE"], handler.GetAuthors(request));
        Assert.Equal(["BOB"], handler.GetDestinations(request));
        Assert.Equal(["M1"], handler.GetIds(request));
    }

    /// <summary>The read and receive receipt handlers each create, recognize and read only their own kind of receipt.</summary>
    [Fact]
    public void ReceiptHandlers_CreateRecognizeAndRead_EachOnlyItsOwnKind()
    {
        FrameMap map = Complete().Build();
        IReceiptFrameHandler read = map.ReadReceipt.Create(null);
        IReceiptFrameHandler receive = map.ReceiveReceipt.Create(null);

        object readReceipt = read.Create(new ReceiptCreateContext { MessageId = "M1", To = "ALICE" });
        object receiveReceipt = receive.Create(new ReceiptCreateContext { MessageId = "M2", To = "ALICE" });

        Assert.True(read.IsValid(readReceipt));
        Assert.False(read.IsValid(receiveReceipt));
        Assert.True(receive.IsValid(receiveReceipt));
        Assert.False(receive.IsValid(readReceipt));
        Assert.Equal("M1", read.GetMessageId(readReceipt));
        Assert.Equal("M2", receive.GetMessageId(receiveReceipt));
    }

    /// <summary>The default serializer builds only the host's frame type, and the default factory calls its parameterless constructor.</summary>
    [Fact]
    public void Defaults_SerializerBuildsOnlyTheMessageType_AndCreateIsNew()
    {
        FrameMap map = Complete().Build();
        IFrameSerializer serializer = map.Serializer.Create(null);
        using IMemoryOwner<byte> own = serializer.Serialize(new TestFrame { MessageId = "M" });
        using IMemoryOwner<byte> other = new ProtobufSerializer().Serialize(new TestHello { Name = "N" });

        Assert.IsType<TestFrame>(serializer.Deserialize(own.Memory, null));
        Assert.Throws<InvalidDataException>(() => serializer.Deserialize(other.Memory, null));
        Assert.IsType<TestFrame>(map.Create());
    }

    /// <summary>A serializer and a factory the host states replace the defaults.</summary>
    [Fact]
    public void Serializer_AndCreate_CanBeReplaced()
    {
        IFrameSerializer serializer = Mock.Of<IFrameSerializer>();
        TestFrame created = new() { MessageId = "CREATED" };

        FrameMap map = Complete().Serializer<IFrameSerializer>().Create(() => created) is FrameBuilder<TestFrame, TestMessagePriority, TestLevel> builder ? builder.Build() : throw new InvalidOperationException();

        Assert.Same(serializer, map.Serializer.Create(new ServiceCollection().AddSingleton(serializer).BuildServiceProvider()));
        Assert.Same(created, map.Create());
    }

    private sealed class AlertsController : IAutoForwardController<TestFrame>
    {
        public string Name { get; } = "Alerts";
        public IReadOnlyList<string> Users { get; } = ["ALICE"];
        public bool Accepts(TestFrame frame) => frame.IsAlert;
    }

    private sealed class OtherController : IAutoForwardController<TestFrame>
    {
        public string Name { get; } = "Other";
        public IReadOnlyList<string> Users { get; } = ["BOB"];
        public bool Accepts(TestFrame frame) => false;
    }

    private sealed class ReplacingAlertsController : IAutoForwardController<TestFrame>
    {
        public string Name { get; } = "ALERTS";
        public IReadOnlyList<string> Users { get; } = ["CAROL"];
        public bool Accepts(TestFrame frame) => true;
    }

    /// <summary>Auto forward controllers are stated by type on the frame builder and describe themselves once instantiated.</summary>
    [Fact]
    public void AutoForward_RegistersControllersByType()
    {
        FrameBuilder<TestFrame, TestMessagePriority, TestLevel> builder = Complete();

        builder.AutoForward<AlertsController>().AutoForward<OtherController>();

        List<AutoForwardControllerDefinition> controllers = [.. builder.AutoForwardControllers.Select(registration => registration.Create(null))];
        Assert.Equal(["Alerts", "Other"], controllers.Select(controller => controller.Name));
        Assert.Equal(["ALICE"], controllers[0].Users);
        Assert.True(controllers[0].Filter(new TestFrame { IsAlert = true }));
        Assert.False(controllers[0].Filter(new TestFrame()));
    }

    /// <summary>A later auto forward controller with the same name as an earlier one replaces it in place once the engine controller resolves them.</summary>
    [Fact]
    public void AutoForward_SameNameReplacesInPlace()
    {
        EngineBuilder engine = EngineBuilder.Build(new TestEngineConfiguration(false, frame => frame.AutoForward<AlertsController>().AutoForward<OtherController>().AutoForward<ReplacingAlertsController>()));

        EngineController controller = new(engine, new CurrentUserProvider());

        Assert.Equal(["ALERTS", "Other"], controller.AutoForwardControllers.Select(definition => definition.Name));
        Assert.Equal(["CAROL"], controller.AutoForwardControllers[0].Users);
    }

    /// <summary>The message handler's addresses are converted between the host's own shape and the engine's, in both directions.</summary>
    [Fact]
    public void MessageHandler_Addresses_ConvertBothWays()
    {
        IMessageFrameHandler handler = Complete().Build().Message.Create(null);
        object message = new TestFrame();

        handler.SetAddresses(message, [
            new MessageAddress { UserName = "A", Type = AddressType.To },
            new MessageAddress { UserName = "B", Type = AddressType.Cc },
            new MessageAddress { UserName = "OMAHA", Type = AddressType.External, Information = "Deliver to Eastside Office" }]);

        TestFrame typed = Assert.IsType<TestFrame>(message);
        Assert.Equal([("A", "To", ""), ("B", "Cc", ""), ("OMAHA", "External", "Deliver to Eastside Office")], typed.Addresses.Select(a => (a.UserName, a.Type, a.Information)));
        Assert.Equal([("A", AddressType.To, ""), ("B", AddressType.Cc, ""), ("OMAHA", AddressType.External, "Deliver to Eastside Office")], handler.GetAddresses(message).Select(a => (a.UserName, a.Type, a.Information)));
    }

    /// <summary>A message goes to its non-external addresses, a retrieval request or receipt to the destination its handler reads, and any other frame nowhere.</summary>
    [Fact]
    public void Route_FollowsTheKindOfFrame()
    {
        EngineController controller = new(EngineBuilder.Build(new TestEngineConfiguration()), new CurrentUserProvider(), null);
        TestFrame message = new() { Addresses = [new TestAddressEntry { UserName = "A", Type = "To" }, new TestAddressEntry { UserName = "OUT", Type = "External" }, new TestAddressEntry { UserName = "a", Type = "Cc" }] };
        TestFrame retrieval = (TestFrame)controller.CreateRetrieval(new RetrievalCriteria(), "SERVER");
        TestFrame receipt = (TestFrame)controller.CreateReadReceipt("M", "BOB");

        Assert.Equal(["A"], controller.Route(message));
        Assert.Equal(["SERVER"], controller.Route(retrieval));
        Assert.Equal(["BOB"], controller.Route(receipt));
        Assert.Empty(controller.Route(new TestFrame { IsHidden = true, IsHeartbeat = true }));
    }
}
