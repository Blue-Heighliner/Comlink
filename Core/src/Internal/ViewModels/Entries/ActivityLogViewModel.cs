namespace BlueHeighliner.Comlink;

/// <summary>ViewModel interface for a daily activity log entry.</summary>
internal interface IActivityLogViewModel
{
    /// <summary>Gets the formatted date string for this log day.</summary>
    string Date { get; }
    /// <summary>Gets the event rows for this day, ordered newest-first.</summary>
    IReadOnlyList<ActivityEventRow> Events { get; }
}

/// <summary>Represents a single formatted row in the activity log view.</summary>
internal sealed class ActivityEventRow
{
    /// <summary>Initializes a new row from the given log entry.</summary>
    /// <param name="entry">The log entry.</param>
    /// <param name="idWidth">The fixed width of the event ID field, or <see langword="null"/> for none.</param>
    public ActivityEventRow(ActivityLogEntry entry, int? idWidth = null)
    {
        TimeText = entry.At.ToString("HH:mm", CultureInfo.InvariantCulture);
        Message = entry.Message;
        IdText = entry.EventId == 0 ? string.Empty : entry.EventId.ToString(CultureInfo.InvariantCulture).PadRight(idWidth ?? 0, '-');
    }

    /// <summary>Gets the formatted timestamp for display.</summary>
    public string TimeText { get; }
    /// <summary>Gets the identifier of the kind of event, padded with hyphens to the log handler's fixed ID width when it states one, or an empty string for an entry that has none.</summary>
    public string IdText { get; }
    /// <summary>Gets the log message text.</summary>
    public string Message { get; }
}

/// <summary>ViewModel for an activity log entry, exposing a date and an ordered list of event rows.</summary>
[ConstructedManually]
internal sealed partial class ActivityLogViewModel : ObservableObject, IActivityLogViewModel
{
    /// <summary>Initializes the ViewModel from the given entity, ordering its entries newest-first.</summary>
    /// <param name="entity">The day's activity log.</param>
    /// <param name="idWidth">The width the log handler fixes the event ID field to, which an ID is padded to with hyphens, or <see langword="null"/> for none.</param>
    public ActivityLogViewModel(ActivityLogEntity entity, int? idWidth = null)
    {
        Date = entity.Date.ToString("dd-MMM-yyyy").ToUpperInvariant();

        Events = entity.EventEntries
            .OrderByDescending(e => e.At)
            .Select(e => new ActivityEventRow(e, idWidth))
            .ToList();
    }

    /// <summary>Gets the formatted date string for this log day.</summary>
    public string Date { get; }
    /// <summary>Gets the list of event rows sorted newest-first.</summary>
    public IReadOnlyList<ActivityEventRow> Events { get; }
}
