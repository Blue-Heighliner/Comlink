namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Control;

/// <summary>
/// Unit tests for <see cref="EngineController"/>'s delegation of the
/// <see cref="IEngineController"/> message fields to the mapping stated by the host, using <see cref="TestEngineController"/>/<see cref="TestFrame"/>
/// as the concrete pair.
/// </summary>
public sealed class MessageFormatTests
{
    private readonly IEngineController format = new TestEngineController();

    /// <summary>FrameType reflects the generic type argument.</summary>
    [Fact]
    public void MessageType_ReflectsGenericArgument()
    {
        Assert.Equal(typeof(TestFrame), format.FrameType);
    }

    /// <summary>CreateFrame produces a new, distinct instance of the concrete frame type each time.</summary>
    [Fact]
    public void CreateMessage_ProducesDistinctInstances()
    {
        object first = format.CreateFrame();
        object second = format.CreateFrame();

        Assert.IsType<TestFrame>(first);
        Assert.IsType<TestFrame>(second);
        Assert.NotSame(first, second);
    }

    /// <summary>Every logical field setter, called through the object-typed IEngineController surface, is readable back through the matching getter.</summary>
    [Fact]
    public void SettersAndGetters_ThroughObjectSurface_RoundTrip()
    {
        object message = format.CreateFrame();
        DateTime sentAt = new(2025, 7, 4, 12, 0, 0, DateTimeKind.Utc);
        List<MessageAddress> addresses =
        [
            new MessageAddress { UserName = "BETA", Type = AddressType.To },
            new MessageAddress { UserName = "GAMMA", Type = AddressType.Cc }
        ];

        ((TestFrame)message).MessageId = "MSG1";
        format.SetFromUser(message, "ALPHA");
        ((TestFrame)message).Body = "World";
        format.SetAddresses(message, addresses);
        ((TestFrame)message).SentAt = sentAt;
        ((TestFrame)message).ReadReceiptMessageId = "MSG0";
        ((TestFrame)message).IsAlert = true;
        ((TestFrame)message).Priority = "LEVEL3";

        Assert.Equal("MSG1", format.GetMessageId(message));
        Assert.Equal("ALPHA", format.GetFromUser(message));
        Assert.Equal("World", format.GetBody(message));
        Assert.Equal(sentAt, format.GetSentAt(message));
        Assert.Equal("MSG0", format.GetReadReceiptMessageId(message));
        Assert.True(format.GetIsAlert(message));
        Assert.Equal(3, format.GetPriority(message));

        List<MessageAddress> roundTripped = format.GetAddresses(message);
        Assert.Equal(2, roundTripped.Count);
        Assert.Equal("BETA", roundTripped[0].UserName);
        Assert.Equal(AddressType.To, roundTripped[0].Type);
        Assert.Equal("GAMMA", roundTripped[1].UserName);
        Assert.Equal(AddressType.Cc, roundTripped[1].Type);
    }

    /// <summary>Values set directly on the concrete TestFrame are visible through the object-typed IEngineController getters, confirming the explicit interface implementation casts to the same instance rather than a copy.</summary>
    [Fact]
    public void ObjectSurface_ReadsBackFieldsSetDirectlyOnConcreteType()
    {
        TestFrame concrete = new() { MessageId = "DIRECT", FromUser = "DELTA" };

        Assert.Equal("DIRECT", format.GetMessageId(concrete));
        Assert.Equal("DELTA", format.GetFromUser(concrete));
    }
}
