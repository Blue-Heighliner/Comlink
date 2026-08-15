namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>Converts a <see cref="DestinationStatus"/> value to a colored brush for status text display.</summary>
[ExcludeFromCodeCoverage]
public sealed class StatusForegroundConverter : IValueConverter
{
    /// <summary>Gets the shared singleton instance.</summary>
    public static readonly StatusForegroundConverter Instance = new();

    private readonly SolidColorBrush redBrush = new(Color.Parse("#E06C75"));
    private readonly SolidColorBrush greenBrush = new(Color.Parse("#98C379"));
    private readonly SolidColorBrush defaultBrush = new(Color.Parse("#858585"));

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DestinationStatus status)
        {
            return status switch
            {
                DestinationStatus.Failed => redBrush,
                DestinationStatus.Confirmed or DestinationStatus.Read or DestinationStatus.Received => greenBrush,
                _ => defaultBrush
            };
        }
        return defaultBrush;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
