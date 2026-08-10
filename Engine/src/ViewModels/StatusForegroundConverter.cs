namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>Converts a <see cref="DestinationStatus"/> value to a colored brush for status text display.</summary>
[ExcludeFromCodeCoverage]
public sealed class StatusForegroundConverter : IValueConverter
{
    /// <summary>Gets the shared singleton instance.</summary>
    public static readonly StatusForegroundConverter Instance = new();

    private static readonly IBrush RedBrush = new SolidColorBrush(Color.Parse("#E06C75"));
    private static readonly IBrush GreenBrush = new SolidColorBrush(Color.Parse("#98C379"));
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#858585"));

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DestinationStatus status)
        {
            return status switch
            {
                DestinationStatus.Failed => RedBrush,
                DestinationStatus.Confirmed => GreenBrush,
                _ => DefaultBrush
            };
        }
        return DefaultBrush;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
