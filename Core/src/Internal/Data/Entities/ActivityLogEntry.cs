namespace BlueHeighliner.Comlink;

/// <summary>A single timestamped event within an <see cref="ActivityLogEntity"/>.</summary>
internal sealed class ActivityLogEntry
{
    /// <summary>UTC timestamp when this event was recorded.</summary>
    public DateTime At { get; set; }
    /// <summary>Human-readable description of the event.</summary>
    public string Message { get; set; } = string.Empty;
}
