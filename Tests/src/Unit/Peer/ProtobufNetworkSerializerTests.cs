namespace BlueHeighliner.Comlink.Tests.Unit.Peer;

/// <summary>Unit tests for <see cref="ProtobufNetworkSerializer"/> protobuf round-trips and its self-describing envelope.</summary>
public sealed class ProtobufNetworkSerializerTests
{
    [ProtoContract]
    private sealed class OtherDto
    {
        [ProtoMember(1)] public string Name { get; set; } = string.Empty;
        [ProtoMember(2)] public int Count { get; set; }
    }

    private static readonly INetworkSerializer serializer = new ProtobufNetworkSerializer();

    /// <summary>TestMessage round-trips all fields including nested addresses.</summary>
    [Fact]
    public void TestMessage_SerializeDeserialize_RoundTrip()
    {
        DateTime sentAt = new(2025, 7, 4, 12, 0, 0, DateTimeKind.Utc);
        TestMessage original = new()
        {
            MessageId = "MSG123",
            FromUser = "ALPHA",
            Subject = "Hello",
            Body = "World",
            SentAt = sentAt,
            Addresses =
            [
                new TestAddressEntry { UserName = "BETA", Type = "To" },
                new TestAddressEntry { UserName = "GAMMA", Type = "Cc" }
            ]
        };

        using IMemoryOwner<byte> buf = serializer.Serialize(original);
        TestMessage? decoded = serializer.Deserialize(buf.Memory) as TestMessage;

        Assert.NotNull(decoded);
        Assert.Equal(original.MessageId, decoded.MessageId);
        Assert.Equal(original.FromUser, decoded.FromUser);
        Assert.Equal(original.Subject, decoded.Subject);
        Assert.Equal(original.Body, decoded.Body);
        Assert.Equal(original.SentAt, decoded.SentAt);
        Assert.Equal(2, decoded.Addresses.Count);
        Assert.Equal("BETA", decoded.Addresses[0].UserName);
        Assert.Equal("Cc", decoded.Addresses[1].Type);
    }

    /// <summary>Different message types serialized by the same serializer each come back as their own type, with no type argument given to Deserialize.</summary>
    [Fact]
    public void DifferentTypes_SerializeDeserialize_EachRoundTripsAsItsOwnType()
    {
        using IMemoryOwner<byte> otherBuf = serializer.Serialize(new OtherDto { Name = "hello", Count = 42 });
        using IMemoryOwner<byte> testBuf = serializer.Serialize(new TestMessage { MessageId = "M1" });

        OtherDto? other = Assert.IsType<OtherDto>(serializer.Deserialize(otherBuf.Memory));
        TestMessage? test = Assert.IsType<TestMessage>(serializer.Deserialize(testBuf.Memory));

        Assert.Equal("hello", other.Name);
        Assert.Equal(42, other.Count);
        Assert.Equal("M1", test.MessageId);
    }

    /// <summary>Serialize produces a non-empty byte sequence for a populated message.</summary>
    [Fact]
    public void Serialize_ProducesNonEmptyBytes()
    {
        TestMessage msg = new() { MessageId = "x", FromUser = "SOURCE", Subject = "s" };
        using IMemoryOwner<byte> buf = serializer.Serialize(msg);
        Assert.True(buf.Memory.Length > 0);
    }

    /// <summary>Every payload is wrapped in the outer envelope, which names the value's runtime type and nests its own encoding.</summary>
    [Fact]
    public void Serialize_WrapsValueInEnvelopeNamingItsType()
    {
        using IMemoryOwner<byte> buf = serializer.Serialize(new TestMessage { MessageId = "M1" });

        ProtobufEnvelope envelope = Serializer.Deserialize<ProtobufEnvelope>((ReadOnlyMemory<byte>)buf.Memory);

        Assert.Equal(typeof(TestMessage).AssemblyQualifiedName, envelope.TypeName);
        Assert.Equal("M1", Serializer.Deserialize<TestMessage>((ReadOnlyMemory<byte>)envelope.Payload).MessageId);
    }

    /// <summary>With no envelope there is nothing to determine a type from, so empty data deserializes to null rather than a default instance.</summary>
    [Fact]
    public void Deserialize_EmptyData_ReturnsNull()
    {
        Assert.Null(serializer.Deserialize(ReadOnlyMemory<byte>.Empty));
    }

    /// <summary>An envelope naming a type that cannot be resolved deserializes to null rather than throwing.</summary>
    [Fact]
    public void Deserialize_UnknownTypeName_ReturnsNull()
    {
        using MemoryStream stream = new();
        Serializer.Serialize(stream, new ProtobufEnvelope { TypeName = "No.Such.Type, NoSuchAssembly", Payload = [1, 2, 3] });

        Assert.Null(serializer.Deserialize(stream.ToArray()));
    }

    private static byte[] Envelope(string typeName)
    {
        using MemoryStream stream = new();
        Serializer.Serialize(stream, new ProtobufEnvelope { TypeName = typeName, Payload = [] });
        return stream.ToArray();
    }

    /// <summary>A serializer told which types it may build builds those, and only those, whatever type name a sender puts in the envelope.</summary>
    [Fact]
    public void Deserialize_KnownTypes_OnlyBuildsThose()
    {
        using IMemoryOwner<byte> other = serializer.Serialize(new OtherDto { Name = "n" });
        using IMemoryOwner<byte> message = serializer.Serialize(new TestMessage { MessageId = "M1" });
        ProtobufNetworkSerializer restricted = new(typeof(TestMessage));

        Assert.Null(restricted.Deserialize(other.Memory));
        Assert.IsType<TestMessage>(restricted.Deserialize(message.Memory));
        Assert.IsType<OtherDto>(new ProtobufNetworkSerializer(typeof(OtherDto), typeof(TestMessage)).Deserialize(other.Memory));
    }

    /// <summary>A serializer told nothing still refuses a named type that is not a protobuf contract, rather than loading whatever a sender names.</summary>
    [Fact]
    public void Deserialize_UnrestrictedSerializer_RefusesTypesWithoutAContract()
    {
        Assert.Null(serializer.Deserialize(Envelope(typeof(Uri).AssemblyQualifiedName!)));
    }

    /// <summary>An engine controller's serializers build only its own message and packet types.</summary>
    [Fact]
    public void EngineControllerSerializers_OnlyBuildTheirOwnTypes()
    {
        TestPacketEngineController controller = new();
        using IMemoryOwner<byte> other = serializer.Serialize(new OtherDto { Name = "n" });
        using IMemoryOwner<byte> message = serializer.Serialize(new TestMessage { MessageId = "M1" });
        using IMemoryOwner<byte> packet = serializer.Serialize(new TestPacket { Count = 1 });

        Assert.Null(controller.NetworkSerializer.Deserialize(other.Memory));
        Assert.Null(controller.NetworkSerializer.Deserialize(packet.Memory));
        Assert.IsType<TestMessage>(controller.NetworkSerializer.Deserialize(message.Memory));
        Assert.Null(controller.PacketSerializer.Deserialize(message.Memory));
        Assert.IsType<TestPacket>(controller.PacketSerializer.Deserialize(packet.Memory));
    }
}
