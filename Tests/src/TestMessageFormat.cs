namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test message DTO standing in for a host-supplied <see cref="IMessageFormat.MessageType"/>.</summary>
[ProtoContract]
internal sealed class TestMessage
{
    /// <summary>Application-level message identifier.</summary>
    [ProtoMember(1)] public string MessageId { get; set; } = string.Empty;
    /// <summary>Site name of the sender.</summary>
    [ProtoMember(2)] public string FromSite { get; set; } = string.Empty;
    /// <summary>Message subject line.</summary>
    [ProtoMember(3)] public string Subject { get; set; } = string.Empty;
    /// <summary>Message body text.</summary>
    [ProtoMember(4)] public string Body { get; set; } = string.Empty;
    /// <summary>Address list associated with the message.</summary>
    [ProtoMember(5)] public List<TestAddressEntry> Addresses { get; set; } = [];
    /// <summary>UTC timestamp when the message was originally sent.</summary>
    [ProtoMember(6)] public DateTime SentAt { get; set; }
}

/// <summary>A single address entry within a <see cref="TestMessage"/>.</summary>
[ProtoContract]
internal sealed class TestAddressEntry
{
    /// <summary>Site name of the addressee.</summary>
    [ProtoMember(1)] public string SiteName { get; set; } = string.Empty;
    /// <summary>Address type (e.g. <c>"To"</c>, <c>"Cc"</c>).</summary>
    [ProtoMember(2)] public string Type { get; set; } = "To";
}

/// <summary>Test <see cref="IMessageFormat"/> implementation backed by <see cref="TestMessage"/>.</summary>
internal sealed class TestMessageFormat : IMessageFormat
{
    /// <inheritdoc />
    public Type MessageType => typeof(TestMessage);
    /// <inheritdoc />
    public object CreateMessage() => new TestMessage();
    /// <inheritdoc />
    public string GetMessageId(object message) => ((TestMessage)message).MessageId;
    /// <inheritdoc />
    public void SetMessageId(object message, string value) => ((TestMessage)message).MessageId = value;
    /// <inheritdoc />
    public string GetFromSite(object message) => ((TestMessage)message).FromSite;
    /// <inheritdoc />
    public void SetFromSite(object message, string value) => ((TestMessage)message).FromSite = value;
    /// <inheritdoc />
    public string GetSubject(object message) => ((TestMessage)message).Subject;
    /// <inheritdoc />
    public void SetSubject(object message, string value) => ((TestMessage)message).Subject = value;
    /// <inheritdoc />
    public string GetBody(object message) => ((TestMessage)message).Body;
    /// <inheritdoc />
    public void SetBody(object message, string value) => ((TestMessage)message).Body = value;

    /// <inheritdoc />
    public List<MessageAddress> GetAddresses(object message) =>
        ((TestMessage)message).Addresses
            .Select(a => new MessageAddress { SiteName = a.SiteName, Type = a.Type.ParseAddressType() })
            .ToList();

    /// <inheritdoc />
    public void SetAddresses(object message, List<MessageAddress> value) =>
        ((TestMessage)message).Addresses = value
            .Select(a => new TestAddressEntry { SiteName = a.SiteName, Type = a.Type.ToString() })
            .ToList();

    /// <inheritdoc />
    public DateTime GetSentAt(object message) => ((TestMessage)message).SentAt;
    /// <inheritdoc />
    public void SetSentAt(object message, DateTime value) => ((TestMessage)message).SentAt = value;
}
