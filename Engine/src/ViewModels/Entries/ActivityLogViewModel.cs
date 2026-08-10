namespace BlueHeighliner.Comlink.Engine.ViewModels.Entries;

/// <summary>ViewModel interface for a daily activity log entry.</summary>
public interface IActivityLogViewModel
{
    /// <summary>Gets the formatted date string for this log day.</summary>
    string Date { get; }
    /// <summary>Gets the event rows for this day, ordered newest-first.</summary>
    IReadOnlyList<ActivityEventRow> Events { get; }
}

/// <summary>Represents a single formatted row in the activity log view.</summary>
public sealed class ActivityEventRow
{
    /// <summary>Gets the formatted timestamp for display.</summary>
    public string TimeText { get; }
    /// <summary>Gets the log message text.</summary>
    public string Message { get; }

    /// <summary>Initializes a new row from the given log entry.</summary>
    public ActivityEventRow(ActivityLogEntry entry)
    {
        TimeText = entry.At.ToString("dd-MMM-yyyy HH:mm").ToUpperInvariant();
        Message = entry.Message;
    }
}

/// <summary>ViewModel for an activity log entry, exposing a date and an ordered list of event rows.</summary>
public sealed partial class ActivityLogViewModel : ObservableObject, IActivityLogViewModel
{
    /// <summary>Gets the formatted date string for this log day.</summary>
    public string Date { get; }
    /// <summary>Gets the list of event rows sorted newest-first.</summary>
    public IReadOnlyList<ActivityEventRow> Events { get; }

    /// <summary>Initializes the ViewModel from the given entity, merging legacy and structured event data.</summary>
    public ActivityLogViewModel(ActivityLogEntity entity)
    {
        Date = entity.Date.ToString("dd-MMM-yyyy").ToUpperInvariant();

        IEnumerable<ActivityLogEntry> legacy = entity.Events.Select(msg => new ActivityLogEntry { At = entity.Date, Message = msg });
        Events = entity.EventEntries.Concat(legacy)
            .OrderByDescending(e => e.At)
            .Select(e => new ActivityEventRow(e))
            .ToList();
    }
}
