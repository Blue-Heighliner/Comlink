namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Control;

/// <summary>Unit tests for <see cref="MessageBuilder{TMessage}"/> and the <see cref="MessageMap"/> it produces.</summary>
public sealed class MessageBuilderTests
{
    private static MessageBuilder<TestMessage> Complete()
    {
        MessageBuilder<TestMessage> builder = new();
        builder
            .Id(m => m.MessageId, (m, v) => m.MessageId = v)
            .Sender(m => m.FromUser, (m, v) => m.FromUser = v)
            .Subject(m => m.Subject, (m, v) => m.Subject = v)
            .Body(m => m.Body, (m, v) => m.Body = v)
            .Addresses(m => m.Addresses.Select(a => (a.UserName, a.Type.ParseAddressType(), a.Information)), (m, v) => m.Addresses = [.. v.Select(a => new TestAddressEntry { UserName = a.Name, Type = a.Type.ToString(), Information = a.Information })])
            .SentAt(m => m.SentAt, (m, v) => m.SentAt = v)
            .ConfirmationId(m => m.ConfirmationMessageId, (m, v) => m.ConfirmationMessageId = v)
            .IsAlert(m => m.IsAlert, (m, v) => m.IsAlert = v)
            .Priority(m => m.Priority, (m, v) => m.Priority = v)
            .Tag(m => m.Tag, (m, v) => m.Tag = v)
            .SecurityLevel(m => m.SecurityLevel, (m, v) => m.SecurityLevel = v);
        return builder;
    }

    /// <summary>Building fails and names every logical field that was not mapped.</summary>
    [Fact]
    public void Build_UnmappedFields_ThrowsNamingThem()
    {
        MessageBuilder<TestMessage> builder = new();
        builder.Id(m => m.MessageId, (m, v) => m.MessageId = v).Body(m => m.Body, (m, v) => m.Body = v);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("TestMessage", error.Message);
        string[] missing = error.Message[(error.Message.IndexOf(':') + 1)..].Split(',', StringSplitOptions.TrimEntries);
        Assert.Equal(["Sender", "Subject", "Addresses", "SentAt", "ConfirmationId", "IsAlert", "Priority", "Tag", "SecurityLevel"], missing);
    }

    /// <summary>The map reads and writes each field of the host's message through the object-typed accessors.</summary>
    [Fact]
    public void Map_ReadsAndWritesEveryField()
    {
        MessageMap map = Complete().Build();
        object message = map.Create();
        DateTime sentAt = new(2026, 9, 28, 1, 2, 3, DateTimeKind.Utc);

        map.SetId(message, "ID");
        map.SetSender(message, "FROM");
        map.SetSubject(message, "SUBJECT");
        map.SetBody(message, "BODY");
        map.SetAddresses(message, [new MessageAddress { UserName = "A", Type = AddressType.Cc }]);
        map.SetSentAt(message, sentAt);
        map.SetConfirmationId(message, "CONFIRMS");
        map.SetIsAlert(message, true);
        map.SetPriority(message, 7);
        map.SetTag(message, "TAG");

        TestMessage typed = Assert.IsType<TestMessage>(message);
        Assert.Equal("ID", typed.MessageId);
        Assert.Equal("ID", map.GetId(message));
        Assert.Equal("FROM", map.GetSender(message));
        Assert.Equal("SUBJECT", map.GetSubject(message));
        Assert.Equal("BODY", map.GetBody(message));
        MessageAddress address = Assert.Single(map.GetAddresses(message));
        Assert.Equal("A", address.UserName);
        Assert.Equal(AddressType.Cc, address.Type);
        Assert.Equal(sentAt, map.GetSentAt(message));
        Assert.Equal("CONFIRMS", map.GetConfirmationId(message));
        Assert.True(map.GetIsAlert(message));
        Assert.Equal(7, map.GetPriority(message));
        Assert.Equal("TAG", map.GetTag(message));
        Assert.Equal(typeof(TestMessage), map.Type);
    }

    /// <summary>The default serializer builds only the host's message type, and the default factory calls its parameterless constructor.</summary>
    [Fact]
    public void Defaults_SerializerBuildsOnlyTheMessageType_AndCreateIsNew()
    {
        MessageMap map = Complete().Build();
        using IMemoryOwner<byte> own = map.Serializer.Serialize(new TestMessage { MessageId = "M" });
        using IMemoryOwner<byte> other = new ProtobufNetworkSerializer().Serialize(new TestHello { Name = "N" });

        Assert.IsType<TestMessage>(map.Serializer.Deserialize(own.Memory));
        Assert.Null(map.Serializer.Deserialize(other.Memory));
        Assert.IsType<TestMessage>(map.Create());
    }

    /// <summary>A serializer and a factory the host states replace the defaults.</summary>
    [Fact]
    public void Serializer_AndCreate_CanBeReplaced()
    {
        INetworkSerializer serializer = Mock.Of<INetworkSerializer>();
        TestMessage created = new() { MessageId = "CREATED" };

        MessageMap map = Complete().Serializer(serializer).Create(() => created) is MessageBuilder<TestMessage> builder ? builder.Build() : throw new InvalidOperationException();

        Assert.Same(serializer, map.Serializer);
        Assert.Same(created, map.Create());
    }

    /// <summary>Mapping a field again replaces the earlier mapping.</summary>
    [Fact]
    public void Map_SameFieldTwice_LastWins()
    {
        MessageBuilder<TestMessage> builder = Complete();
        builder.Subject(m => "replaced", (m, v) => { });

        Assert.Equal("replaced", builder.Build().GetSubject(new TestMessage { Subject = "original" }));
    }

    private sealed class Plain
    {
        public string Id { get; set; } = "";
        public string Sender { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public List<(string Name, AddressType Type, string Information)> Addresses { get; set; } = [];
        public DateTime SentAt { get; set; }
        public string ConfirmationId { get; set; } = "";
        public bool IsAlert { get; set; }
        public int Priority { get; set; }
        public string Tag { get; set; } = "";
        public string SecurityLevel { get; set; } = "";
    }

    /// <summary>Every field whose type already matches can be mapped by naming the property alone, and behaves exactly like the explicit getter and setter.</summary>
    [Fact]
    public void PropertyOverloads_MapEachMatchingFieldWithoutASetter()
    {
        MessageBuilder<Plain> builder = new();
        builder.Id(m => m.Id).Sender(m => m.Sender).Subject(m => m.Subject).Body(m => m.Body)
            .Addresses(m => m.Addresses, (m, v) => m.Addresses = [.. v])
            .SentAt(m => m.SentAt).ConfirmationId(m => m.ConfirmationId).IsAlert(m => m.IsAlert).Priority(m => m.Priority).Tag(m => m.Tag)
            .SecurityLevel(m => m.SecurityLevel);
        MessageMap map = builder.Build();
        object message = map.Create();
        DateTime sentAt = new(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);

        map.SetId(message, "ID");
        map.SetSender(message, "FROM");
        map.SetSubject(message, "SUBJECT");
        map.SetBody(message, "BODY");
        map.SetSentAt(message, sentAt);
        map.SetConfirmationId(message, "CONFIRMS");
        map.SetIsAlert(message, true);
        map.SetPriority(message, 4);
        map.SetTag(message, "TAG");
        map.SetSecurityLevel(message, "RESTRICTED");

        Plain typed = Assert.IsType<Plain>(message);
        Assert.Equal(("ID", "FROM", "SUBJECT", "BODY"), (typed.Id, typed.Sender, typed.Subject, typed.Body));
        Assert.Equal(sentAt, typed.SentAt);
        Assert.Equal(("CONFIRMS", true, 4, "TAG"), (typed.ConfirmationId, typed.IsAlert, typed.Priority, typed.Tag));
        Assert.Equal("RESTRICTED", typed.SecurityLevel);
        Assert.Equal("ID", map.GetId(message));
        Assert.Equal(4, map.GetPriority(message));
        Assert.Equal("RESTRICTED", map.GetSecurityLevel(message));
    }

    /// <summary>A property overload that cannot supply a setter fails when the mapping is stated, naming the expression.</summary>
    [Fact]
    public void PropertyOverload_ReadOnlyProperty_ThrowsImmediately()
    {
        MessageBuilder<TestMessage> builder = new();

        Assert.Throws<ArgumentException>(() => builder.Id(m => m.MessageId.ToUpper()));
    }

    /// <summary>The addresses are converted between the host's own shape and the engine's, in both directions, through name and type tuples.</summary>
    [Fact]
    public void Addresses_ConvertsThroughTuples()
    {
        MessageMap map = Complete().Build();
        object message = map.Create();

        map.SetAddresses(message, [
            new MessageAddress { UserName = "A", Type = AddressType.To },
            new MessageAddress { UserName = "B", Type = AddressType.Cc },
            new MessageAddress { UserName = "OMAHA", Type = AddressType.External, Information = "Deliver to Eastside Office" }]);

        TestMessage typed = Assert.IsType<TestMessage>(message);
        Assert.Equal([("A", "To", ""), ("B", "Cc", ""), ("OMAHA", "External", "Deliver to Eastside Office")], typed.Addresses.Select(a => (a.UserName, a.Type, a.Information)));
        List<MessageAddress> read = map.GetAddresses(message);
        Assert.Equal([("A", AddressType.To, ""), ("B", AddressType.Cc, ""), ("OMAHA", AddressType.External, "Deliver to Eastside Office")], read.Select(a => (a.UserName, a.Type, a.Information)));
    }

    /// <summary>A host with no use for per-address instructions can map addresses with the two-tuple overload; every address reads back with an empty <c>Information</c>.</summary>
    [Fact]
    public void Addresses_TwoTupleOverload_MapsWithEmptyInformation()
    {
        MessageBuilder<TestMessage> builder = new();
        builder.Id(m => m.MessageId, (m, v) => m.MessageId = v).Sender(m => "", (m, v) => { }).Subject(m => "", (m, v) => { }).Body(m => "", (m, v) => { })
            .Addresses(
                m => m.Addresses.Select(a => (a.UserName, a.Type.ParseAddressType())),
                (m, v) => m.Addresses = [.. v.Select(a => new TestAddressEntry { UserName = a.Name, Type = a.Type.ToString() })])
            .SentAt(m => default, (m, v) => { }).ConfirmationId(m => "", (m, v) => { }).IsAlert(m => false, (m, v) => { }).Priority(m => 0, (m, v) => { }).Tag(m => "", (m, v) => { })
            .SecurityLevel(m => "", (m, v) => { });
        MessageMap map = builder.Build();
        object message = map.Create();

        map.SetAddresses(message, [new MessageAddress { UserName = "A", Type = AddressType.To, Information = "ignored" }]);

        MessageAddress address = Assert.Single(map.GetAddresses(message));
        Assert.Equal(("A", AddressType.To, ""), (address.UserName, address.Type, address.Information));
    }
}
