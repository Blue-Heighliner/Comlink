namespace BlueHeighliner.Comlink.Engine.Models;

/// <summary>Identifies a recipient site and its address role on a message or draft.</summary>
public sealed class MessageAddress
{
    /// <summary>Name of the recipient site.</summary>
    public required string SiteName { get; init; }
    /// <summary>Address role (To or Cc) for this recipient.</summary>
    public required AddressType Type { get; init; }
}
