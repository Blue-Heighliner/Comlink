namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Demonstrates injecting a custom frame DTO. Field names are deliberately unlike the engine's own
/// logical field names (<c>Id</c> vs frame id, <c>Sender</c> vs sender user, <c>Text</c> vs body, <c>Recipients</c> with a <see cref="bool"/> flag vs an address-type enum) to show
/// that <see cref="EngineConfiguration"/>'s frame mapping is what maps the engine's logical
/// fields onto this type's real ones — the engine itself never assumes any particular field name or
/// shape, and requires a host to state its frame type since it has no built-in one of its own.
/// </summary>
[ProtoContract]
public sealed class Frame
{
    /// <summary>Application-level message identifier.</summary>
    [ProtoMember(1)] public string Id { get; set; } = string.Empty;
    /// <summary>User name of the sender.</summary>
    [ProtoMember(2)] public string Sender { get; set; } = string.Empty;
    /// <summary>Message body text.</summary>
    [ProtoMember(4)] public string Text { get; set; } = string.Empty;
    /// <summary>Recipient list.</summary>
    [ProtoMember(5)] public List<Recipient> Recipients { get; set; } = [];
    /// <summary>UTC timestamp when the message was originally sent.</summary>
    [ProtoMember(6)] public DateTime Timestamp { get; set; }
    /// <summary>Message ID this message is a read receipt for; empty for an ordinary message.</summary>
    [ProtoMember(7)] public string ReadMessageId { get; set; } = string.Empty;
    /// <summary>Integer value of the priority level of this message, the value of a MessagePriority member.</summary>
    [ProtoMember(9)] public int Importance { get; set; }
    /// <summary>Short user-inputted tag identifying the type of this message.</summary>
    [ProtoMember(10)] public string Category { get; set; } = string.Empty;
    /// <summary>Integer value of the message level this message was sent at, the value of a MessageLevel member, or <see langword="null"/> for none.</summary>
    [ProtoMember(11)] public int? Confidentiality { get; set; }
    /// <summary>Integer value of the message aspect this message carries, the value of a MessageAspect member, or <see langword="null"/> for none.</summary>
    [ProtoMember(22)] public int? Protection { get; set; }
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
    /// <summary>Whether this frame is a message the user reads and that is stored, rather than only network traffic.</summary>
    [ProtoMember(18)] public bool IsMessage { get; set; }
    /// <summary>Whether this frame is a read receipt for the message in <see cref="ReadMessageId"/>.</summary>
    [ProtoMember(19)] public bool IsReadReceipt { get; set; }
    /// <summary>The message this frame is a receive receipt for, when <see cref="IsReceiveReceipt"/>.</summary>
    [ProtoMember(20)] public string ReceivedMessageId { get; set; } = string.Empty;
    /// <summary>Whether this frame is a receive receipt for the message in <see cref="ReceivedMessageId"/>.</summary>
    [ProtoMember(21)] public bool IsReceiveReceipt { get; set; }
}

/// <summary>A single recipient entry within a <see cref="Frame"/>.</summary>
[ProtoContract]
public sealed class Recipient
{
    /// <summary>User name of the addressee.</summary>
    [ProtoMember(1)] public string User { get; set; } = string.Empty;
    /// <summary>How the recipient is addressed: <c>TO</c>, <c>CC</c>, or <c>OUTSIDE</c> for an address outside the system.</summary>
    [ProtoMember(2)] public string Kind { get; set; } = "TO";
    /// <summary>Custom instructions attached to the recipient, such as <c>Deliver to Eastside Office</c>.</summary>
    [ProtoMember(3)] public string Note { get; set; } = string.Empty;
}
