namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>Converts a boolean value to a <see cref="FontWeight"/>, mapping <see langword="true"/> to <see cref="FontWeight.Bold"/> and <see langword="false"/> to <see cref="FontWeight.Normal"/>.</summary>
[ExcludeFromCodeCoverage]
internal sealed class BoolToFontWeightConverter : IValueConverter
{
    /// <summary>Gets the shared singleton instance.</summary>
    public static readonly BoolToFontWeightConverter Instance = new();

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? FontWeight.Bold : FontWeight.Normal;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
