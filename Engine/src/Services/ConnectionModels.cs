namespace BlueHeighliner.Comlink.Engine.Services;

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
    /// <summary>Whether this message is an alert; see <see cref="Control.IMessageFormat.GetIsAlert"/>.</summary>
    public bool IsAlert { get; set; }
    /// <summary>Priority number of this message; see <see cref="Control.IMessageFormat.GetPriority"/>.</summary>
    public int Priority { get; set; }
    /// <summary>Tag identifying the type of this message; see <see cref="Control.IMessageFormat.GetTag"/>.</summary>
    public string Tag { get; set; } = string.Empty;
}

/// <summary>Represents a single addressee in a send or receive operation.</summary>
public sealed class AddressRequest
{
    /// <summary>User name of the addressee.</summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>Address type (e.g. <c>"To"</c>, <c>"Cc"</c>).</summary>
    public string Type { get; set; } = "To";
}

/// <summary>Delivery outcome for a single destination user after a send operation.</summary>
public sealed class UserDeliveryResult
{
    /// <summary>Name of the destination user.</summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>
    /// Whether the message was successfully delivered to this user. For a remote user this reflects OFT's own
    /// delivery status — the underlying send only completes once OFT has fully acknowledged the message — so
    /// a successful send here means the message is already fully delivered, not merely queued. For the sending
    /// user addressing itself, delivery happens in-process with no network round-trip and is always successful.
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

/// <summary>Request payload for routing a message to one or more addressed users via <see cref="IMessageRoutingService.Route"/>.</summary>
public sealed class SendMessagePayload
{
    /// <summary>Message subject line.</summary>
    public string Subject { get; set; } = string.Empty;
    /// <summary>Message body text.</summary>
    public string Body { get; set; } = string.Empty;
    /// <summary>List of recipient addresses for this message.</summary>
    public List<AddressPayload> Addresses { get; set; } = [];
    /// <summary>Whether this message is an alert; see <see cref="Control.IMessageFormat.GetIsAlert"/>.</summary>
    public bool IsAlert { get; set; }
    /// <summary>Priority number of this message; see <see cref="Control.IMessageFormat.GetPriority"/>.</summary>
    public int Priority { get; set; }
    /// <summary>Tag identifying the type of this message; see <see cref="Control.IMessageFormat.GetTag"/>.</summary>
    public string Tag { get; set; } = string.Empty;
}

/// <summary>A single recipient address entry used in <see cref="SendMessagePayload"/>.</summary>
public sealed class AddressPayload
{
    /// <summary>Name of the addressed user.</summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>Address type (e.g. "To", "Cc").</summary>
    public string Type { get; set; } = "To";
}
