namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Control;

/// <summary>Unit tests for <see cref="FrameBuilder{TFrame}"/> and the <see cref="FrameMap"/> it produces.</summary>
public sealed class FrameBuilderTests
{
    private static FrameBuilder<TestFrame> Complete()
    {
        FrameBuilder<TestFrame> builder = new();
        builder
            .Id(m => m.MessageId, (m, v) => m.MessageId = v)
            .Sender(m => m.FromUser, (m, v) => m.FromUser = v)
            .Addresses(m => m.Addresses.Select(a => (a.UserName, a.Type.ParseAddressType(), a.Information)), (m, v) => m.Addresses = [.. v.Select(a => new TestAddressEntry { UserName = a.Name, Type = a.Type.ToString(), Information = a.Information })])
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
        FrameBuilder<TestFrame> builder = new();
        builder.Id(m => m.MessageId, (m, v) => m.MessageId = v);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("TestFrame", error.Message);
        string[] missing = error.Message[(error.Message.IndexOf(':') + 1)..].Split(',', StringSplitOptions.TrimEntries);
        Assert.Equal(["Sender", "Addresses", "Message", "Retrieval", "ReadReceipt", "ReceiveReceipt"], missing);
    }

    /// <summary>The map reads and writes each common field of the host's frame through the object-typed accessors.</summary>
    [Fact]
    public void Map_ReadsAndWritesEveryCommonField()
    {
        FrameMap map = Complete().Build();
        object message = map.Create();
        map.SetId(message, "ID");
        map.SetSender(message, "FROM");
        map.SetAddresses(message, [new MessageAddress { UserName = "A", Type = AddressType.Cc }]);

        TestFrame typed = Assert.IsType<TestFrame>(message);
        Assert.Equal("ID", typed.MessageId);
        Assert.Equal("ID", map.GetId(message));
        Assert.Equal("FROM", map.GetSender(message));
        MessageAddress address = Assert.Single(map.GetAddresses(message));
        Assert.Equal("A", address.UserName);
        Assert.Equal(AddressType.Cc, address.Type);
        Assert.Equal(typeof(TestFrame), map.Type);
    }

    /// <summary>The message handler the host states creates a message from its content, recognizes it, and reads its content back.</summary>
    [Fact]
    public void MessageHandler_CreatesRecognizesAndReads()
    {
        IMessageFrameHandler handler = Complete().Build().Message.Create(null);
        DateTime sentAt = new(2026, 9, 28, 1, 2, 3, DateTimeKind.Utc);

        object message = handler.Create(new MessageCreateContext { SentAt = sentAt, Body = "BODY", IsAlert = true, Priority = 7, Tag = "TAG", SecurityLevel = "SECRET" });

        Assert.IsType<TestFrame>(message);
        Assert.True(handler.IsValid(message));
        Assert.False(handler.IsValid(new TestFrame { IsHidden = true }));
        Assert.Equal((sentAt, "BODY", true, 7, "TAG", "SECRET"), (handler.GetSentAt(message), handler.GetBody(message), handler.GetIsAlert(message), handler.GetPriority(message), handler.GetTag(message), handler.GetSecurityLevel(message)));
    }

    /// <summary>The retrieval handler the host states creates a request from its criteria, recognizes it, and reads the criteria back.</summary>
    [Fact]
    public void RetrievalHandler_CreatesRecognizesAndReads()
    {
        IRetrievalFrameHandler handler = Complete().Build().Retrieval.Create(null);
        DateTime from = new(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);

        object request = handler.Create(new RetrievalCreateContext { From = from, Authors = ["ALICE"], Destinations = ["BOB"], Ids = ["M1"] });

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

        object readReceipt = read.Create(new ReceiptCreateContext { MessageId = "M1" });
        object receiveReceipt = receive.Create(new ReceiptCreateContext { MessageId = "M2" });

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

        FrameMap map = Complete().Serializer<IFrameSerializer>().Create(() => created) is FrameBuilder<TestFrame> builder ? builder.Build() : throw new InvalidOperationException();

        Assert.Same(serializer, map.Serializer.Create(new ServiceCollection().AddSingleton(serializer).BuildServiceProvider()));
        Assert.Same(created, map.Create());
    }

    /// <summary>Mapping a field again replaces the earlier mapping.</summary>
    [Fact]
    public void Map_SameFieldTwice_LastWins()
    {
        FrameBuilder<TestFrame> builder = Complete();
        builder.Sender(m => "replaced", (m, v) => { });

        Assert.Equal("replaced", builder.Build().GetSender(new TestFrame { FromUser = "original" }));
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
        FrameBuilder<TestFrame> builder = Complete();

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

    private sealed class Plain
    {
        public string Id { get; set; } = "";
        public string Sender { get; set; } = "";
        public List<(string Name, AddressType Type, string Information)> Addresses { get; set; } = [];
    }

    /// <summary>Every field whose type already matches can be mapped by naming the property alone, and behaves exactly like the explicit getter and setter.</summary>
    [Fact]
    public void PropertyOverloads_MapEachMatchingFieldWithoutASetter()
    {
        FrameBuilder<Plain> builder = new();
        builder.Id(m => m.Id).Sender(m => m.Sender)
            .Addresses(m => m.Addresses, (m, v) => m.Addresses = [.. v])
            .Message<IMessageHandler<Plain>>().Retrieval<IRetrievalHandler<Plain>>().ReadReceipt<IReadReceiptHandler<Plain>>().ReceiveReceipt<IReceiveReceiptHandler<Plain>>();
        FrameMap map = builder.Build();
        object message = map.Create();
        map.SetId(message, "ID");
        map.SetSender(message, "FROM");

        Plain typed = Assert.IsType<Plain>(message);
        Assert.Equal(("ID", "FROM"), (typed.Id, typed.Sender));
        Assert.Equal("ID", map.GetId(message));
    }

    /// <summary>A property overload that cannot supply a setter fails when the mapping is stated, naming the expression.</summary>
    [Fact]
    public void PropertyOverload_ReadOnlyProperty_ThrowsImmediately()
    {
        FrameBuilder<TestFrame> builder = new();

        Assert.Throws<ArgumentException>(() => builder.Id(m => m.MessageId.ToUpper()));
    }

    /// <summary>The addresses are converted between the host's own shape and the engine's, in both directions, through name and type tuples.</summary>
    [Fact]
    public void Addresses_ConvertsThroughTuples()
    {
        FrameMap map = Complete().Build();
        object message = map.Create();

        map.SetAddresses(message, [
            new MessageAddress { UserName = "A", Type = AddressType.To },
            new MessageAddress { UserName = "B", Type = AddressType.Cc },
            new MessageAddress { UserName = "OMAHA", Type = AddressType.External, Information = "Deliver to Eastside Office" }]);

        TestFrame typed = Assert.IsType<TestFrame>(message);
        Assert.Equal([("A", "To", ""), ("B", "Cc", ""), ("OMAHA", "External", "Deliver to Eastside Office")], typed.Addresses.Select(a => (a.UserName, a.Type, a.Information)));
        List<MessageAddress> read = map.GetAddresses(message);
        Assert.Equal([("A", AddressType.To, ""), ("B", AddressType.Cc, ""), ("OMAHA", AddressType.External, "Deliver to Eastside Office")], read.Select(a => (a.UserName, a.Type, a.Information)));
    }

    /// <summary>A host with no use for per-address instructions can map addresses with the two-tuple overload; every address reads back with an empty <c>Information</c>.</summary>
    [Fact]
    public void Addresses_TwoTupleOverload_MapsWithEmptyInformation()
    {
        FrameBuilder<TestFrame> builder = new();
        builder.Id(m => m.MessageId, (m, v) => m.MessageId = v).Sender(m => "", (m, v) => { })
            .Addresses(
                m => m.Addresses.Select(a => (a.UserName, a.Type.ParseAddressType())),
                (m, v) => m.Addresses = [.. v.Select(a => new TestAddressEntry { UserName = a.Name, Type = a.Type.ToString() })])
            .Message<TestMessageHandler>().Retrieval<TestRetrievalHandler>().ReadReceipt<TestReadReceiptHandler>().ReceiveReceipt<TestReceiveReceiptHandler>();
        FrameMap map = builder.Build();
        object message = map.Create();

        map.SetAddresses(message, [new MessageAddress { UserName = "A", Type = AddressType.To, Information = "ignored" }]);

        MessageAddress address = Assert.Single(map.GetAddresses(message));
        Assert.Equal(("A", AddressType.To, ""), (address.UserName, address.Type, address.Information));
    }
}
