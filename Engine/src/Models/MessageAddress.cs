namespace BlueHeighliner.Comlink.Engine.Models;

/// <summary>Identifies a recipient user and its address role on a message or draft.</summary>
public sealed class MessageAddress
{
    /// <summary>Name of the recipient user.</summary>
    public required string UserName { get; init; }
    /// <summary>Address role (To or Cc) for this recipient.</summary>
    public required AddressType Type { get; init; }
}
