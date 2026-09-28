namespace BlueHeighliner.Comlink.Data.Entities;

/// <summary>Persisted address entry on a message or draft, recording a recipient user and address type.</summary>
internal sealed class AddressData
{
    /// <summary>Name of the recipient user.</summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>Address role string ("To", "Cc" or "External").</summary>
    public string Type { get; set; } = "To";
    /// <summary>Custom instructions attached to the address, or an empty string when there are none.</summary>
    public string Information { get; set; } = string.Empty;
}
