namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test message DTO standing in for a host-supplied <see cref="IEngineController.MessageType"/>.</summary>
[ProtoContract]
public sealed class TestMessage
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
    /// <summary>Priority number of this message.</summary>
    [ProtoMember(9)] public int Priority { get; set; }
    /// <summary>Tag identifying the type of this message.</summary>
    [ProtoMember(10)] public string Tag { get; set; } = string.Empty;
}

/// <summary>A single address entry within a <see cref="TestMessage"/>.</summary>
[ProtoContract]
public sealed class TestAddressEntry
{
    /// <summary>User name of the addressee.</summary>
    [ProtoMember(1)] public string UserName { get; set; } = string.Empty;
    /// <summary>Address type (e.g. <c>"To"</c>, <c>"Cc"</c>).</summary>
    [ProtoMember(2)] public string Type { get; set; } = "To";
    /// <summary>Custom instructions attached to the address.</summary>
    [ProtoMember(3)] public string Information { get; set; } = string.Empty;
}

/// <summary>
/// Test <see cref="IEngineController"/> built from <see cref="TestEngineConfiguration"/>, which maps the logical message fields onto
/// <see cref="TestMessage"/>, with the engine's defaults for everything else. Not <see langword="sealed"/> because tests
/// override single members, and Moq subclasses it with <c>CallBase = true</c>.
/// </summary>
internal class TestEngineController() : EngineController(EngineBuilder.Build(new TestEngineConfiguration()), new CurrentUserProvider())
{
}
