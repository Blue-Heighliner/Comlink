namespace BlueHeighliner.Comlink.ViewModels;

/// <summary>
/// Converts an address's stored canonical type name (<c>"To"</c>, <c>"Cc"</c> or <c>"External"</c>) and the current
/// <see cref="AddressTypeOption"/> list into that type's display label, so the per-address badge in the draft
/// editor reflects the same overridable label as the address type picker.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class AddressTypeLabelConverter : IMultiValueConverter
{
    /// <summary>Gets the shared singleton instance.</summary>
    public static readonly AddressTypeLabelConverter Instance = new();

    /// <inheritdoc />
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => values is [string typeName, IReadOnlyList<AddressTypeOption> options, ..]
            ? options.GetLabel(typeName.ParseAddressType())
            : values.Count > 0 ? values[0] : null;
}
