namespace BlueHeighliner.Comlink.Services;

/// <summary>Request payload for routing a message to one or more addressed users via <see cref="IMessageRoutingService.Route"/>.</summary>
internal sealed class SendMessagePayload
{
    /// <summary>Message subject line.</summary>
    public string Subject { get; set; } = string.Empty;
    /// <summary>Message body text.</summary>
    public string Body { get; set; } = string.Empty;
    /// <summary>List of recipient addresses for this message.</summary>
    public List<AddressPayload> Addresses { get; set; } = [];
    /// <summary>Whether this message is an alert; see <see cref="Control.IEngineController.GetIsAlert"/>.</summary>
    public bool IsAlert { get; set; }
    /// <summary>Priority number of this message; see <see cref="Control.IEngineController.GetPriority"/>.</summary>
    public int Priority { get; set; }
    /// <summary>Tag identifying the type of this message; see <see cref="Control.IEngineController.GetTag"/>.</summary>
    public string Tag { get; set; } = string.Empty;
}

/// <summary>A single recipient address entry used in <see cref="SendMessagePayload"/>.</summary>
internal sealed class AddressPayload
{
    /// <summary>Name of the addressed user.</summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>Address type (e.g. "To", "Cc").</summary>
    public string Type { get; set; } = "To";
}
