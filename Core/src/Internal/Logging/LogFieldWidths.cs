namespace BlueHeighliner.Comlink;

/// <summary>The fixed widths the host gave the fields of a log line (see <see cref="ILogHandler"/>); <see langword="null"/> is not fixed.</summary>
/// <param name="Category">The width of the category field.</param>
/// <param name="User">The width of the user field.</param>
/// <param name="Id">The width of the event ID field.</param>
internal sealed record LogFieldWidths(int? Category, int? User, int? Id)
{
    /// <summary>Gets the widths of a host that states no log handler: no field is fixed.</summary>
    public static LogFieldWidths None { get; } = new(null, null, null);
}
