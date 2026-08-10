namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>Provides sort label text for the sort toggle button.</summary>
public sealed class SortLabelConverter
{
    /// <summary>Gets the shared singleton instance.</summary>
    public static readonly SortLabelConverter Instance = new();

    /// <summary>Returns a human-readable sort label for the given boolean sort flag.</summary>
    /// <param name="value">The sort flag value; <see langword="true"/> for alphabetical, otherwise most-recent-first.</param>
    /// <param name="targetType">Ignored.</param>
    /// <param name="parameter">Ignored.</param>
    /// <param name="culture">Ignored.</param>
    /// <returns>The display label for the current sort mode.</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Sort: A-Z" : "Sort: Recent";

    /// <summary>Not supported.</summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
