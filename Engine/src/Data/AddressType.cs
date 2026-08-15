namespace BlueHeighliner.Comlink.Engine.Data;

/// <summary>Specifies the role of a recipient address on a message or draft.</summary>
public enum AddressType
{
    /// <summary>Primary recipient.</summary>
    To,
    /// <summary>Carbon-copy recipient.</summary>
    Cc
}

/// <summary>Extension methods for <see cref="AddressType"/>.</summary>
public static class AddressTypeExtensions
{
    /// <summary>Parses a role string (e.g. <c>"To"</c>, <c>"Cc"</c>) into an <see cref="AddressType"/>, defaulting to <see cref="AddressType.To"/> for an unrecognized value.</summary>
    public static AddressType ParseAddressType(this string type)
        => Enum.TryParse(type, ignoreCase: true, out AddressType parsed) ? parsed : AddressType.To;
}
