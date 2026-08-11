namespace BlueHeighliner.Comlink.Tests.Control;

/// <summary>
/// Unit tests for <see cref="MessageFormat{TMessage}"/>'s generic-to-<see cref="IMessageFormat"/> casting
/// and delegation, using <see cref="TestMessageFormat"/>/<see cref="TestMessage"/> as the concrete pair.
/// </summary>
public sealed class MessageFormatTests
{
    private static readonly IMessageFormat Format = new TestMessageFormat();

    /// <summary>MessageType reflects the generic type argument.</summary>
    [Fact]
    public void MessageType_ReflectsGenericArgument()
    {
        Assert.Equal(typeof(TestMessage), Format.MessageType);
    }

    /// <summary>CreateMessage produces a new, distinct instance of the concrete message type each time.</summary>
    [Fact]
    public void CreateMessage_ProducesDistinctInstances()
    {
        object first = Format.CreateMessage();
        object second = Format.CreateMessage();

        Assert.IsType<TestMessage>(first);
        Assert.IsType<TestMessage>(second);
        Assert.NotSame(first, second);
    }

    /// <summary>Every logical field setter, called through the object-typed IMessageFormat surface, is readable back through the matching getter.</summary>
    [Fact]
    public void SettersAndGetters_ThroughObjectSurface_RoundTrip()
    {
        object message = Format.CreateMessage();
        DateTime sentAt = new(2025, 7, 4, 12, 0, 0, DateTimeKind.Utc);
        List<MessageAddress> addresses =
        [
            new MessageAddress { UserName = "BETA", Type = AddressType.To },
            new MessageAddress { UserName = "GAMMA", Type = AddressType.Cc }
        ];

        Format.SetMessageId(message, "MSG1");
        Format.SetFromUser(message, "ALPHA");
        Format.SetSubject(message, "Hello");
        Format.SetBody(message, "World");
        Format.SetAddresses(message, addresses);
        Format.SetSentAt(message, sentAt);
        Format.SetConfirmationMessageId(message, "MSG0");
        Format.SetIsAlert(message, true);

        Assert.Equal("MSG1", Format.GetMessageId(message));
        Assert.Equal("ALPHA", Format.GetFromUser(message));
        Assert.Equal("Hello", Format.GetSubject(message));
        Assert.Equal("World", Format.GetBody(message));
        Assert.Equal(sentAt, Format.GetSentAt(message));
        Assert.Equal("MSG0", Format.GetConfirmationMessageId(message));
        Assert.True(Format.GetIsAlert(message));

        List<MessageAddress> roundTripped = Format.GetAddresses(message);
        Assert.Equal(2, roundTripped.Count);
        Assert.Equal("BETA", roundTripped[0].UserName);
        Assert.Equal(AddressType.To, roundTripped[0].Type);
        Assert.Equal("GAMMA", roundTripped[1].UserName);
        Assert.Equal(AddressType.Cc, roundTripped[1].Type);
    }

    /// <summary>Values set directly on the concrete TestMessage are visible through the object-typed IMessageFormat getters, confirming the explicit interface implementation casts to the same instance rather than a copy.</summary>
    [Fact]
    public void ObjectSurface_ReadsBackFieldsSetDirectlyOnConcreteType()
    {
        TestMessage concrete = new() { MessageId = "DIRECT", FromUser = "DELTA" };

        Assert.Equal("DIRECT", Format.GetMessageId(concrete));
        Assert.Equal("DELTA", Format.GetFromUser(concrete));
    }
}
