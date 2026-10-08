namespace BlueHeighliner.Comlink;

/// <summary>Identifies a recipient user and its address role on a message or draft.</summary>
public sealed record MessageAddress
{
    /// <summary>Gets the name of the recipient user.</summary>
    public required string UserName { get; init; }

    /// <summary>Gets the address role (<see cref="AddressType.To"/>, <see cref="AddressType.Cc"/> or <see cref="AddressType.External"/>) for this recipient.</summary>
    public required AddressType Type { get; init; }

    /// <summary>Gets the custom instructions the sender attached to this address (for example <c>Deliver to Eastside Office</c>), or an empty string when there are none.</summary>
    public string Information { get; init; } = string.Empty;
}
