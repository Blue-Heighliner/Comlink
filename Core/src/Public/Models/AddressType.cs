namespace BlueHeighliner.Comlink;

/// <summary>Specifies the role of a recipient address on a message or draft.</summary>
public enum AddressType
{
    /// <summary>Primary recipient.</summary>
    To,
    /// <summary>Carbon-copy recipient.</summary>
    Cc,
    /// <summary>
    /// A recipient outside the system: the message is only to be delivered to this address by other means. It is
    /// information for the user and nothing more; the engine takes no action for it, so it is never routed, expanded as a
    /// group, or given a delivery status.
    /// </summary>
    External
}

/// <summary>Extension methods for <see cref="AddressType"/>.</summary>
internal static class AddressTypeExtensions
{
    /// <summary>Parses a role string (e.g. <c>"To"</c>, <c>"Cc"</c>, <c>"External"</c>) into an <see cref="AddressType"/>, defaulting to <see cref="AddressType.To"/> for an unrecognized value.</summary>
    public static AddressType ParseAddressType(this string type)
        => Enum.TryParse(type, ignoreCase: true, out AddressType parsed) ? parsed : AddressType.To;
}
