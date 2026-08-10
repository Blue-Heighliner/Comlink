namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Demonstrates injecting a custom message DTO. Field names are deliberately unlike the engine's own
/// logical field names (<c>Id</c> vs message id, <c>Sender</c> vs sender site, <c>Title</c> vs subject,
/// <c>Text</c> vs body, <c>Recipients</c> with a <see cref="bool"/> flag vs an address-type enum) to show
/// that <see cref="SampleMessageFormat"/> is what maps the engine's logical fields onto this type's real
/// ones — the engine itself never assumes any particular field name or shape, and requires a host to
/// register an <see cref="IMessageFormat"/> since it has no built-in message type of its own.
/// </summary>
[ProtoContract]
public sealed class SampleMessage
{
    /// <summary>Application-level message identifier.</summary>
    [ProtoMember(1)] public string Id { get; set; } = string.Empty;
    /// <summary>Site name of the sender.</summary>
    [ProtoMember(2)] public string Sender { get; set; } = string.Empty;
    /// <summary>Message subject line.</summary>
    [ProtoMember(3)] public string Title { get; set; } = string.Empty;
    /// <summary>Message body text.</summary>
    [ProtoMember(4)] public string Text { get; set; } = string.Empty;
    /// <summary>Recipient list.</summary>
    [ProtoMember(5)] public List<SampleRecipient> Recipients { get; set; } = [];
    /// <summary>UTC timestamp when the message was originally sent.</summary>
    [ProtoMember(6)] public DateTime Timestamp { get; set; }
}

/// <summary>A single recipient entry within a <see cref="SampleMessage"/>.</summary>
[ProtoContract]
public sealed class SampleRecipient
{
    /// <summary>Site name of the addressee.</summary>
    [ProtoMember(1)] public string Site { get; set; } = string.Empty;
    /// <summary><see langword="true"/> for a carbon-copy recipient; <see langword="false"/> for a primary recipient.</summary>
    [ProtoMember(2)] public bool IsCc { get; set; }
}

/// <summary>Maps the engine's logical message fields onto <see cref="SampleMessage"/>.</summary>
internal sealed class SampleMessageFormat : IMessageFormat
{
    /// <inheritdoc />
    public Type MessageType => typeof(SampleMessage);
    /// <inheritdoc />
    public object CreateMessage() => new SampleMessage();
    /// <inheritdoc />
    public string GetMessageId(object message) => ((SampleMessage)message).Id;
    /// <inheritdoc />
    public void SetMessageId(object message, string value) => ((SampleMessage)message).Id = value;
    /// <inheritdoc />
    public string GetFromSite(object message) => ((SampleMessage)message).Sender;
    /// <inheritdoc />
    public void SetFromSite(object message, string value) => ((SampleMessage)message).Sender = value;
    /// <inheritdoc />
    public string GetSubject(object message) => ((SampleMessage)message).Title;
    /// <inheritdoc />
    public void SetSubject(object message, string value) => ((SampleMessage)message).Title = value;
    /// <inheritdoc />
    public string GetBody(object message) => ((SampleMessage)message).Text;
    /// <inheritdoc />
    public void SetBody(object message, string value) => ((SampleMessage)message).Text = value;

    /// <inheritdoc />
    public List<MessageAddress> GetAddresses(object message) =>
        ((SampleMessage)message).Recipients
            .Select(r => new MessageAddress { SiteName = r.Site, Type = r.IsCc ? AddressType.Cc : AddressType.To })
            .ToList();

    /// <inheritdoc />
    public void SetAddresses(object message, List<MessageAddress> value) =>
        ((SampleMessage)message).Recipients = value
            .Select(a => new SampleRecipient { Site = a.SiteName, IsCc = a.Type == AddressType.Cc })
            .ToList();

    /// <inheritdoc />
    public DateTime GetSentAt(object message) => ((SampleMessage)message).Timestamp;
    /// <inheritdoc />
    public void SetSentAt(object message, DateTime value) => ((SampleMessage)message).Timestamp = value;
}
