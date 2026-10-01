namespace BlueHeighliner.Comlink.Services;

/// <summary>Data carried by the message-received event raised when a peer delivers an inbound message.</summary>
public sealed class MessageReceivedEvent
{
    /// <summary>Application-level identifier of the received message.</summary>
    public string MessageId { get; set; } = string.Empty;
    /// <summary>User name of the sender.</summary>
    public string FromUser { get; set; } = string.Empty;
    /// <summary>Message subject line.</summary>
    public string Subject { get; set; } = string.Empty;
    /// <summary>Message body text.</summary>
    public string Body { get; set; } = string.Empty;
    /// <summary>Address list associated with the message.</summary>
    public List<AddressRequest> Addresses { get; set; } = [];
    /// <summary>UTC timestamp when the message was originally sent.</summary>
    public DateTime SentAt { get; set; }
    /// <summary>Whether this message is an alert; see <see cref="IFrameBuilder{TFrame}"/>.</summary>
    public bool IsAlert { get; set; }
    /// <summary>Priority number of this message; see <see cref="IFrameBuilder{TFrame}"/>.</summary>
    public int Priority { get; set; }
    /// <summary>Tag identifying the type of this message; see <see cref="IFrameBuilder{TFrame}"/>.</summary>
    public string Tag { get; set; } = string.Empty;
    /// <summary>Security level name this message was sent at, or an empty string when no security levels are configured; see <see cref="IFrameBuilder{TFrame}"/>.</summary>
    public string SecurityLevel { get; set; } = string.Empty;
}

/// <summary>Represents a single addressee in a send or receive operation.</summary>
public sealed class AddressRequest
{
    /// <summary>User name of the addressee.</summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>Address type (<c>"To"</c>, <c>"Cc"</c> or <c>"External"</c>); an external address is information for the user and is never delivered.</summary>
    public string Type { get; set; } = "To";
    /// <summary>Custom instructions attached to the address (for example <c>Deliver to Eastside Office</c>), or an empty string when there are none.</summary>
    public string Information { get; set; } = string.Empty;
}

/// <summary>Delivery outcome for a single destination user after a send operation.</summary>
public sealed class UserDeliveryResult
{
    /// <summary>Name of the destination user.</summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>
    /// Whether the message was successfully delivered to this user. For a remote user this reflects the peer
    /// transport's own delivery status — the underlying send only completes once it has fully acknowledged the
    /// message — so a successful send here means the message is already fully delivered, not merely queued.
    /// For the sending user addressing itself, delivery happens in-process with no network round-trip and is
    /// always successful.
    /// </summary>
    public bool Success { get; set; }
    /// <summary>Names of the groups in the address list that contained this user.</summary>
    public List<string> AddressedVia { get; set; } = [];
}

/// <summary>Result returned from a send-message operation, including per-user delivery outcomes.</summary>
public sealed class SendMessageResult
{
    /// <summary>Application-level identifier assigned to the sent message.</summary>
    public string MessageId { get; set; } = string.Empty;
    /// <summary>Per-user delivery results for the send operation.</summary>
    public List<UserDeliveryResult> UserResults { get; set; } = [];
}

/// <summary>Data carried by the delivery-status-changed event when a message's delivery state transitions.</summary>
public sealed class DeliveryStatusChangedEvent
{
    /// <summary>Application-level identifier of the affected message.</summary>
    public string MessageId { get; set; } = string.Empty;
    /// <summary>Name of the user whose delivery status changed.</summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>New delivery status for this user.</summary>
    public DestinationStatus Status { get; set; }
    /// <summary>Aggregate delivery status across all destination users, or <see langword="null"/> if not yet determined.</summary>
    public DestinationStatus? OverallStatus { get; set; }
}
