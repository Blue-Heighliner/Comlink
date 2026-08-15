namespace BlueHeighliner.Comlink.Tests.Peer;

/// <summary>Unit tests for <see cref="PeerSerializer"/> protobuf round-trips.</summary>
public sealed class PeerSerializerTests
{
    [ProtoContract]
    private sealed class OtherDto
    {
        [ProtoMember(1)] public string Name { get; set; } = string.Empty;
        [ProtoMember(2)] public int Count { get; set; }
    }

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

        using OwnedBuffer buf = PeerSerializer.Serialize(original);
        TestMessage? decoded = PeerSerializer.Deserialize(typeof(TestMessage), buf.Memory) as TestMessage;

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

    /// <summary>Serialize/Deserialize work for any protobuf-net type resolved at runtime, not just TestMessage.</summary>
    [Fact]
    public void OtherType_SerializeDeserialize_RoundTrip()
    {
        OtherDto original = new() { Name = "hello", Count = 42 };

        using OwnedBuffer buf = PeerSerializer.Serialize(original);
        OtherDto? decoded = PeerSerializer.Deserialize(typeof(OtherDto), buf.Memory) as OtherDto;

        Assert.NotNull(decoded);
        Assert.Equal("hello", decoded.Name);
        Assert.Equal(42, decoded.Count);
    }

    /// <summary>Serialize produces a non-empty byte sequence for a populated message.</summary>
    [Fact]
    public void Serialize_ProducesNonEmptyBytes()
    {
        TestMessage msg = new() { MessageId = "x", FromUser = "SOURCE", Subject = "s" };
        using OwnedBuffer buf = PeerSerializer.Serialize(msg);
        Assert.True(buf.Memory.Length > 0);
    }

    /// <summary>Deserializing an empty span returns a default instance (protobuf-net treats empty bytes as all-default values).</summary>
    [Fact]
    public void Deserialize_EmptySpan_ReturnsDefaultInstance()
    {
        TestMessage? result = PeerSerializer.Deserialize(typeof(TestMessage), ReadOnlyMemory<byte>.Empty) as TestMessage;
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.MessageId);
        Assert.Equal(string.Empty, result.FromUser);
        Assert.Empty(result.Addresses);
    }
}
