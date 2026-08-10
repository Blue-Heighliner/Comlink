namespace BlueHeighliner.Comlink.Engine.Data.Entities;

/// <summary>Persisted delivery outcome for a single recipient site on an outbound message.</summary>
public sealed class DeliveryStatus
{
    /// <summary>Name of the destination site.</summary>
    public string SiteName { get; set; } = string.Empty;
    /// <summary>Current delivery status for this site.</summary>
    public DestinationStatus Status { get; set; }
    /// <summary>Names of the groups in the message's address list that contained this site, enabling "SITE (GROUP)" display labels.</summary>
    public List<string> AddressedVia { get; set; } = [];
}
