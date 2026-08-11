namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test message DTO standing in for a host-supplied <see cref="IMessageFormat.MessageType"/>.</summary>
[ProtoContract]
internal sealed class TestMessage
{
    /// <summary>Application-level message identifier.</summary>
    [ProtoMember(1)] public string MessageId { get; set; } = string.Empty;
    /// <summary>User name of the sender.</summary>
    [ProtoMember(2)] public string FromUser { get; set; } = string.Empty;
    /// <summary>Message subject line.</summary>
    [ProtoMember(3)] public string Subject { get; set; } = string.Empty;
    /// <summary>Message body text.</summary>
    [ProtoMember(4)] public string Body { get; set; } = string.Empty;
    /// <summary>Address list associated with the message.</summary>
    [ProtoMember(5)] public List<TestAddressEntry> Addresses { get; set; } = [];
    /// <summary>UTC timestamp when the message was originally sent.</summary>
    [ProtoMember(6)] public DateTime SentAt { get; set; }
    /// <summary>Message ID this message is a user-read confirmation for; empty for an ordinary message.</summary>
    [ProtoMember(7)] public string ConfirmationMessageId { get; set; } = string.Empty;
    /// <summary>Whether this message is an alert.</summary>
    [ProtoMember(8)] public bool IsAlert { get; set; }
}

/// <summary>A single address entry within a <see cref="TestMessage"/>.</summary>
[ProtoContract]
internal sealed class TestAddressEntry
{
    /// <summary>User name of the addressee.</summary>
    [ProtoMember(1)] public string UserName { get; set; } = string.Empty;
    /// <summary>Address type (e.g. <c>"To"</c>, <c>"Cc"</c>).</summary>
    [ProtoMember(2)] public string Type { get; set; } = "To";
}

/// <summary>Test <see cref="IMessageFormat"/> implementation backed by <see cref="TestMessage"/>, with no casting required.</summary>
internal sealed class TestMessageFormat : MessageFormat<TestMessage>
{
    /// <inheritdoc />
    protected override string GetMessageId(TestMessage message) => message.MessageId;
    /// <inheritdoc />
    protected override void SetMessageId(TestMessage message, string value) => message.MessageId = value;
    /// <inheritdoc />
    protected override string GetFromUser(TestMessage message) => message.FromUser;
    /// <inheritdoc />
    protected override void SetFromUser(TestMessage message, string value) => message.FromUser = value;
    /// <inheritdoc />
    protected override string GetSubject(TestMessage message) => message.Subject;
    /// <inheritdoc />
    protected override void SetSubject(TestMessage message, string value) => message.Subject = value;
    /// <inheritdoc />
    protected override string GetBody(TestMessage message) => message.Body;
    /// <inheritdoc />
    protected override void SetBody(TestMessage message, string value) => message.Body = value;

    /// <inheritdoc />
    protected override List<MessageAddress> GetAddresses(TestMessage message) =>
        message.Addresses
            .Select(a => new MessageAddress { UserName = a.UserName, Type = a.Type.ParseAddressType() })
            .ToList();

    /// <inheritdoc />
    protected override void SetAddresses(TestMessage message, List<MessageAddress> value) =>
        message.Addresses = value
            .Select(a => new TestAddressEntry { UserName = a.UserName, Type = a.Type.ToString() })
            .ToList();

    /// <inheritdoc />
    protected override DateTime GetSentAt(TestMessage message) => message.SentAt;
    /// <inheritdoc />
    protected override void SetSentAt(TestMessage message, DateTime value) => message.SentAt = value;
    /// <inheritdoc />
    protected override string GetConfirmationMessageId(TestMessage message) => message.ConfirmationMessageId;
    /// <inheritdoc />
    protected override void SetConfirmationMessageId(TestMessage message, string value) => message.ConfirmationMessageId = value;
    /// <inheritdoc />
    protected override bool GetIsAlert(TestMessage message) => message.IsAlert;
    /// <inheritdoc />
    protected override void SetIsAlert(TestMessage message, bool value) => message.IsAlert = value;
}
