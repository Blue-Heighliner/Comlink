namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>Converts a hex color string (e.g., <c>"#98C379"</c>) to a <see cref="SolidColorBrush"/>.</summary>
[ExcludeFromCodeCoverage]
internal sealed class ColorHexToBrushConverter : IValueConverter
{
    /// <summary>Gets the shared singleton instance.</summary>
    public static readonly ColorHexToBrushConverter Instance = new();

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new SolidColorBrush(Color.Parse(value as string ?? "#858585"));

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
