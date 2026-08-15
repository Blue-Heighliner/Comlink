namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Demonstrates injecting a custom message DTO. Field names are deliberately unlike the engine's own
/// logical field names (<c>Id</c> vs message id, <c>Sender</c> vs sender user, <c>Title</c> vs subject,
/// <c>Text</c> vs body, <c>Recipients</c> with a <see cref="bool"/> flag vs an address-type enum) to show
/// that <see cref="SampleEngineController"/>'s message-field overrides are what map the engine's logical
/// fields onto this type's real ones — the engine itself never assumes any particular field name or
/// shape, and requires a host to supply an <see cref="IEngineController"/> since it has no built-in
/// message type of its own.
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
    /// <summary>Priority number of this message.</summary>
    [ProtoMember(9)] public int Importance { get; set; }
    /// <summary>Short user-inputted tag identifying the type of this message.</summary>
    [ProtoMember(10)] public string Category { get; set; } = string.Empty;
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
