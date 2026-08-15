namespace BlueHeighliner.Comlink.Tests.Control;

/// <summary>
/// Unit tests for <see cref="DefaultEngineController{TMessage}"/>'s generic-to-<see cref="IEngineController"/>
/// message-field casting and delegation, using <see cref="TestEngineController"/>/<see cref="TestMessage"/>
/// as the concrete pair.
/// </summary>
public sealed class MessageFormatTests
{
    private readonly IEngineController format = new TestEngineController();

    /// <summary>MessageType reflects the generic type argument.</summary>
    [Fact]
    public void MessageType_ReflectsGenericArgument()
    {
        Assert.Equal(typeof(TestMessage), format.MessageType);
    }

    /// <summary>CreateMessage produces a new, distinct instance of the concrete message type each time.</summary>
    [Fact]
    public void CreateMessage_ProducesDistinctInstances()
    {
        object first = format.CreateMessage();
        object second = format.CreateMessage();

        Assert.IsType<TestMessage>(first);
        Assert.IsType<TestMessage>(second);
        Assert.NotSame(first, second);
    }

    /// <summary>Every logical field setter, called through the object-typed IEngineController surface, is readable back through the matching getter.</summary>
    [Fact]
    public void SettersAndGetters_ThroughObjectSurface_RoundTrip()
    {
        object message = format.CreateMessage();
        DateTime sentAt = new(2025, 7, 4, 12, 0, 0, DateTimeKind.Utc);
        List<MessageAddress> addresses =
        [
            new MessageAddress { UserName = "BETA", Type = AddressType.To },
            new MessageAddress { UserName = "GAMMA", Type = AddressType.Cc }
        ];

        format.SetMessageId(message, "MSG1");
        format.SetFromUser(message, "ALPHA");
        format.SetSubject(message, "Hello");
        format.SetBody(message, "World");
        format.SetAddresses(message, addresses);
        format.SetSentAt(message, sentAt);
        format.SetConfirmationMessageId(message, "MSG0");
        format.SetIsAlert(message, true);
        format.SetPriority(message, 3);

        Assert.Equal("MSG1", format.GetMessageId(message));
        Assert.Equal("ALPHA", format.GetFromUser(message));
        Assert.Equal("Hello", format.GetSubject(message));
        Assert.Equal("World", format.GetBody(message));
        Assert.Equal(sentAt, format.GetSentAt(message));
        Assert.Equal("MSG0", format.GetConfirmationMessageId(message));
        Assert.True(format.GetIsAlert(message));
        Assert.Equal(3, format.GetPriority(message));

        List<MessageAddress> roundTripped = format.GetAddresses(message);
        Assert.Equal(2, roundTripped.Count);
        Assert.Equal("BETA", roundTripped[0].UserName);
        Assert.Equal(AddressType.To, roundTripped[0].Type);
        Assert.Equal("GAMMA", roundTripped[1].UserName);
        Assert.Equal(AddressType.Cc, roundTripped[1].Type);
    }

    /// <summary>Values set directly on the concrete TestMessage are visible through the object-typed IEngineController getters, confirming the explicit interface implementation casts to the same instance rather than a copy.</summary>
    [Fact]
    public void ObjectSurface_ReadsBackFieldsSetDirectlyOnConcreteType()
    {
        TestMessage concrete = new() { MessageId = "DIRECT", FromUser = "DELTA" };

        Assert.Equal("DIRECT", format.GetMessageId(concrete));
        Assert.Equal("DELTA", format.GetFromUser(concrete));
    }
}
