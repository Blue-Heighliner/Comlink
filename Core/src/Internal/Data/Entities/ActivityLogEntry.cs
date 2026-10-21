namespace BlueHeighliner.Comlink;

/// <summary>A single timestamped event within an <see cref="ActivityLogEntity"/>.</summary>
internal sealed class ActivityLogEntry
{
    /// <summary>UTC timestamp when this event was recorded.</summary>
    public DateTime At { get; set; }
    /// <summary>Human-readable description of the event.</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>The identifier of the kind of event this is (see <see cref="LogEvents"/>, such as <c>CL56</c>), or empty for an entry that has none.</summary>
    public string EventId { get; set; } = string.Empty;
}
