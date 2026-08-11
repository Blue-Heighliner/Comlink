namespace BlueHeighliner.Comlink.Engine.Data.Entities;

/// <summary>Persisted delivery outcome for a single recipient user on an outbound message.</summary>
public sealed class DeliveryStatus
{
    /// <summary>Name of the destination user.</summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>Current delivery status for this user.</summary>
    public DestinationStatus Status { get; set; }
    /// <summary>Names of the groups in the message's address list that contained this user, enabling "USER (GROUP)" display labels.</summary>
    public List<string> AddressedVia { get; set; } = [];
}
