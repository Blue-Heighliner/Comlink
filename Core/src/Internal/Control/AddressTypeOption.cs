namespace BlueHeighliner.Comlink.Control;

/// <summary>A single address type paired with its display label.</summary>
internal sealed record AddressTypeOption
{
    /// <summary>Gets the address type this option represents.</summary>
    public required AddressType Type { get; init; }
    /// <summary>Gets the display label shown to the user (e.g. in the address type picker and the message view's section headers).</summary>
    public required string Label { get; init; }
}

/// <summary>Extension helpers for looking up display information from a set of <see cref="AddressTypeOption"/> values.</summary>
internal static class AddressTypeOptionExtensions
{
    /// <summary>Returns the display <see cref="AddressTypeOption.Label"/> matching <paramref name="type"/>, or the plain enum name if no option in <paramref name="options"/> matches.</summary>
    public static string GetLabel(this IReadOnlyList<AddressTypeOption> options, AddressType type)
        => options.FirstOrDefault(o => o.Type == type)?.Label ?? type.ToString();
}
