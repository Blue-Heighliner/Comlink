namespace BlueHeighliner.Comlink;

/// <summary>Request payload for routing a message to one or more addressed users via <see cref="IMessageRoutingService.Route"/>.</summary>
internal sealed class SendMessagePayload
{
    /// <summary>Message body text.</summary>
    public string Body { get; set; } = string.Empty;
    /// <summary>List of recipient addresses for this message.</summary>
    public List<AddressPayload> Addresses { get; set; } = [];
    /// <summary>Whether this message is an alert; see <see cref="IEngineController.GetIsAlert"/>.</summary>
    /// <summary>Priority level of this message, a member of the enum the host stated for its priorities; <see langword="null"/> or one that is not a configured level is the lowest level.</summary>
    public Enum? Priority { get; set; }
    /// <summary>Tag identifying the type of this message; see <see cref="IEngineController.GetTag"/>.</summary>
    public string Tag { get; set; } = string.Empty;
    /// <summary>Security level name this message is sent at; see <see cref="IEngineController.GetSecurityLevel"/>.</summary>
    public string SecurityLevel { get; set; } = string.Empty;
}

/// <summary>A single recipient address entry used in <see cref="SendMessagePayload"/>.</summary>
internal sealed class AddressPayload
{
    /// <summary>Name of the addressed user.</summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>Address type ("To", "Cc" or "External").</summary>
    public string Type { get; set; } = "To";
    /// <summary>Custom instructions attached to the address, or an empty string when there are none.</summary>
    public string Information { get; set; } = string.Empty;
}
