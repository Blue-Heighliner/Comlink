namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test frame DTO standing in for a host-supplied <see cref="IEngineController.FrameType"/>.</summary>
[ProtoContract]
public sealed class TestFrame
{
    /// <summary>Application-level message identifier.</summary>
    [ProtoMember(1)] public string MessageId { get; set; } = string.Empty;
    /// <summary>User name of the sender.</summary>
    [ProtoMember(2)] public string FromUser { get; set; } = string.Empty;
    /// <summary>Message body text.</summary>
    [ProtoMember(4)] public string Body { get; set; } = string.Empty;
    /// <summary>Address list associated with the message.</summary>
    [ProtoMember(5)] public List<TestAddressEntry> Addresses { get; set; } = [];
    /// <summary>UTC timestamp when the message was originally sent.</summary>
    [ProtoMember(6)] public DateTime SentAt { get; set; }
    /// <summary>Message ID this message is a read receipt for; empty for an ordinary message.</summary>
    [ProtoMember(7)] public string ReadReceiptMessageId { get; set; } = string.Empty;
    /// <summary>Message ID this message is a receive receipt for; empty for an ordinary message.</summary>
    [ProtoMember(19)] public string ReceiveReceiptMessageId { get; set; } = string.Empty;
    /// <summary>Whether this message is an alert.</summary>
    [ProtoMember(8)] public bool IsAlert { get; set; }
    /// <summary>Priority number of this message.</summary>
    [ProtoMember(9)] public int Priority { get; set; }
    /// <summary>Tag identifying the type of this message.</summary>
    [ProtoMember(10)] public string Tag { get; set; } = string.Empty;
    /// <summary>Security level name this message was sent at.</summary>
    [ProtoMember(11)] public string SecurityLevel { get; set; } = string.Empty;
    /// <summary>Whether this message is a retrieval request to a storage server.</summary>
    [ProtoMember(12)] public bool IsRetrieval { get; set; }
    /// <summary>Retrieval request lower sent-time bound.</summary>
    [ProtoMember(13)] public DateTime? RetrievalFrom { get; set; }
    /// <summary>Retrieval request upper sent-time bound.</summary>
    [ProtoMember(14)] public DateTime? RetrievalTo { get; set; }
    /// <summary>Retrieval request sender names.</summary>
    [ProtoMember(15)] public List<string> RetrievalAuthors { get; set; } = [];
    /// <summary>Retrieval request addressee names.</summary>
    [ProtoMember(16)] public List<string> RetrievalDestinations { get; set; } = [];
    /// <summary>Retrieval request message identifiers.</summary>
    [ProtoMember(17)] public List<string> RetrievalIds { get; set; } = [];
    /// <summary>Whether this frame is only network traffic rather than a message (stored inversely so an ordinary test frame is a message).</summary>
    [ProtoMember(18)] public bool IsHidden { get; set; }
}

/// <summary>A single address entry within a <see cref="TestFrame"/>.</summary>
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
/// <see cref="TestFrame"/>, with the engine's defaults for everything else. Not <see langword="sealed"/> because tests
/// override single members, and Moq subclasses it with <c>CallBase = true</c>.
/// </summary>
internal class TestEngineController : EngineController
{
    /// <summary>Creates a controller over the default test configuration and an empty network.</summary>
    public TestEngineController()
        : this(null)
    {
    }

    /// <summary>Creates a controller over the default test configuration and <paramref name="network"/>.</summary>
    /// <param name="network">The network configuration describing the users, or <see langword="null"/> for an empty one.</param>
    public TestEngineController(NetworkConfig? network)
        : base(EngineBuilder.Build(new TestEngineConfiguration()), new CurrentUserProvider(), network)
    {
    }
}
