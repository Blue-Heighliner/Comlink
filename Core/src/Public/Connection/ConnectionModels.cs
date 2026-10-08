namespace BlueHeighliner.Comlink;

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

/// <summary>Result returned from a send-message operation. How the message fares afterwards is reported destination by destination, as delivery status changes (see <see cref="IEngineConnection.DeliveryStatusChanged"/>).</summary>
public sealed class SendMessageResult
{
    /// <summary>Application-level identifier assigned to the sent message.</summary>
    public string MessageId { get; set; } = string.Empty;
    /// <summary>Whether the sent message is an alert, as the host's draft handler decided from its other properties.</summary>
    public bool IsAlert { get; set; }
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
