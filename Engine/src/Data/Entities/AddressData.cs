namespace BlueHeighliner.Comlink.Engine.Data.Entities;

/// <summary>Persisted address entry on a message or draft, recording a recipient user and address type.</summary>
public sealed class AddressData
{
    /// <summary>Name of the recipient user.</summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>Address role string (e.g. "To" or "Cc").</summary>
    public string Type { get; set; } = "To";
}
