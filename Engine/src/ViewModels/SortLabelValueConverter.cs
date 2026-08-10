namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>Converts a boolean sort flag to a human-readable label for the sort toggle button.</summary>
[ExcludeFromCodeCoverage]
internal sealed class SortLabelValueConverter : IValueConverter
{
    /// <summary>Gets the shared singleton instance.</summary>
    public static readonly SortLabelValueConverter Instance = new();

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Sort: A-Z" : "Sort: Recent";

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
