namespace BlueHeighliner.Comlink.Engine.ViewModels;

/// <summary>ViewModel representing a single row in the entry list panel.</summary>
public sealed partial class EntryItemViewModel : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusColorHex))]
    private DestinationStatus? _overallStatus;

    /// <summary>Gets the unique identifier for this entry (message ID or LiteDB object-id string).</summary>
    public string Id { get; }
    /// <summary>Gets the primary display title for this entry.</summary>
    public string Title { get; }
    /// <summary>Gets an optional secondary line of text shown below the title.</summary>
    public string? SecondaryText { get; }
    /// <summary>Gets an optional formatted timestamp string for display.</summary>
    public string? TimeText { get; }
    /// <summary>Gets a static status string that takes precedence when no overall status is set.</summary>
    public string? FixedStatusText { get; }
    /// <summary>Gets the type of this entry (message, draft, note, or activity).</summary>
    public EntryType EntryType { get; }
    /// <summary>Gets the date used for default chronological sorting.</summary>
    public DateTime SortDate { get; }
    /// <summary>
    /// For <see cref="Data.EntryType.Message"/> entries, <see langword="true"/> when this row represents the
    /// Outbox (sent) record and <see langword="false"/> when it represents the Inbox (received) record. A
    /// self-addressed message has one document of each kind sharing the same <see cref="Id"/>, so this disambiguates
    /// which document to load, move, or delete. Meaningless for other entry types.
    /// </summary>
    public bool IsOutboundMessage { get; }

    /// <summary>Gets the status text to display, derived from the overall delivery status or the fixed status text.</summary>
    public string? StatusText => OverallStatus?.ToString().ToUpperInvariant() ?? FixedStatusText;

    /// <summary>Gets the hex color string for the status text based on delivery outcome.</summary>
    public string StatusColorHex => OverallStatus switch
    {
        DestinationStatus.Failed => "#E06C75",
        DestinationStatus.Confirmed or DestinationStatus.Read or DestinationStatus.Received => "#98C379",
        _ => "#858585"
    };

    /// <summary>Initializes a new entry item row with the given identity and display properties.</summary>
    /// <param name="id">Unique identifier for this entry.</param>
    /// <param name="title">Primary display title.</param>
    /// <param name="entryType">Type of this entry.</param>
    /// <param name="sortDate">Date used for chronological ordering.</param>
    /// <param name="secondaryText">Optional secondary line of text shown below the title.</param>
    /// <param name="timeText">Optional formatted timestamp string.</param>
    /// <param name="fixedStatusText">Optional static status string that takes precedence when no overall status is set.</param>
    /// <param name="isOutboundMessage">For Message entries, whether this row represents the Outbox (sent) record rather than the Inbox (received) record.</param>
    public EntryItemViewModel(string id, string title, EntryType entryType, DateTime sortDate,
        string? secondaryText = null, string? timeText = null, string? fixedStatusText = null, bool isOutboundMessage = false)
    {
        Id = id;
        Title = title;
        EntryType = entryType;
        SortDate = sortDate;
        SecondaryText = secondaryText;
        TimeText = timeText;
        FixedStatusText = fixedStatusText;
        IsOutboundMessage = isOutboundMessage;
    }
}
