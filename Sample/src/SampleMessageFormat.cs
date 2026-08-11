namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Demonstrates injecting a custom message DTO. Field names are deliberately unlike the engine's own
/// logical field names (<c>Id</c> vs message id, <c>Sender</c> vs sender user, <c>Title</c> vs subject,
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
    /// <summary>User name of the sender.</summary>
    [ProtoMember(2)] public string Sender { get; set; } = string.Empty;
    /// <summary>Message subject line.</summary>
    [ProtoMember(3)] public string Title { get; set; } = string.Empty;
    /// <summary>Message body text.</summary>
    [ProtoMember(4)] public string Text { get; set; } = string.Empty;
    /// <summary>Recipient list.</summary>
    [ProtoMember(5)] public List<SampleRecipient> Recipients { get; set; } = [];
    /// <summary>UTC timestamp when the message was originally sent.</summary>
    [ProtoMember(6)] public DateTime Timestamp { get; set; }
    /// <summary>Message ID this message is a user-read confirmation for; empty for an ordinary message.</summary>
    [ProtoMember(7)] public string ConfirmsId { get; set; } = string.Empty;
    /// <summary>Whether this message is an alert.</summary>
    [ProtoMember(8)] public bool Alert { get; set; }
}

/// <summary>A single recipient entry within a <see cref="SampleMessage"/>.</summary>
[ProtoContract]
public sealed class SampleRecipient
{
    /// <summary>User name of the addressee.</summary>
    [ProtoMember(1)] public string User { get; set; } = string.Empty;
    /// <summary><see langword="true"/> for a carbon-copy recipient; <see langword="false"/> for a primary recipient.</summary>
    [ProtoMember(2)] public bool IsCc { get; set; }
}

/// <summary>Maps the engine's logical message fields onto <see cref="SampleMessage"/>, with no casting required.</summary>
internal sealed class SampleMessageFormat : MessageFormat<SampleMessage>
{
    /// <inheritdoc />
    protected override string GetMessageId(SampleMessage message) => message.Id;
    /// <inheritdoc />
    protected override void SetMessageId(SampleMessage message, string value) => message.Id = value;
    /// <inheritdoc />
    protected override string GetFromUser(SampleMessage message) => message.Sender;
    /// <inheritdoc />
    protected override void SetFromUser(SampleMessage message, string value) => message.Sender = value;
    /// <inheritdoc />
    protected override string GetSubject(SampleMessage message) => message.Title;
    /// <inheritdoc />
    protected override void SetSubject(SampleMessage message, string value) => message.Title = value;
    /// <inheritdoc />
    protected override string GetBody(SampleMessage message) => message.Text;
    /// <inheritdoc />
    protected override void SetBody(SampleMessage message, string value) => message.Text = value;

    /// <inheritdoc />
    protected override List<MessageAddress> GetAddresses(SampleMessage message) =>
        message.Recipients
            .Select(r => new MessageAddress { UserName = r.User, Type = r.IsCc ? AddressType.Cc : AddressType.To })
            .ToList();

    /// <inheritdoc />
    protected override void SetAddresses(SampleMessage message, List<MessageAddress> value) =>
        message.Recipients = value
            .Select(a => new SampleRecipient { User = a.UserName, IsCc = a.Type == AddressType.Cc })
            .ToList();

    /// <inheritdoc />
    protected override DateTime GetSentAt(SampleMessage message) => message.Timestamp;
    /// <inheritdoc />
    protected override void SetSentAt(SampleMessage message, DateTime value) => message.Timestamp = value;
    /// <inheritdoc />
    protected override string GetConfirmationMessageId(SampleMessage message) => message.ConfirmsId;
    /// <inheritdoc />
    protected override void SetConfirmationMessageId(SampleMessage message, string value) => message.ConfirmsId = value;
    /// <inheritdoc />
    protected override bool GetIsAlert(SampleMessage message) => message.Alert;
    /// <inheritdoc />
    protected override void SetIsAlert(SampleMessage message, bool value) => message.Alert = value;
}
