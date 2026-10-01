namespace BlueHeighliner.Comlink.ViewModels;

/// <summary>ViewModel representing a single row in the entry list panel.</summary>
internal sealed partial class EntryItemViewModel : ObservableObject
{
    /// <summary>Initializes a new entry item row with the given identity and display properties.</summary>
    /// <param name="id">Unique identifier for this entry.</param>
    /// <param name="title">Primary display title.</param>
    /// <param name="entryType">Type of this entry.</param>
    /// <param name="sortDate">Date used for chronological ordering.</param>
    /// <param name="secondaryText">Optional secondary line of text shown below the title.</param>
    /// <param name="priorityText">Optional priority label shown below <paramref name="secondaryText"/>.</param>
    /// <param name="tagText">Optional tag label shown next to <paramref name="priorityText"/>.</param>
    /// <param name="timeText">Optional formatted timestamp string.</param>
    /// <param name="fixedStatusText">Optional static status string that takes precedence when no overall status is set.</param>
    /// <param name="isOutboundMessage">For Message entries, whether this row represents the Outbox (sent) record rather than the Inbox (received) record.</param>
    /// <param name="securityLevelColorHex">For Message entries, the hex color of the message's security level, or <see langword="null"/> when it has none recognized.</param>
    /// <param name="isAlert">Whether this entry is flagged as an alert; the title renders in red when <see langword="true"/>. Messages and drafts only.</param>
    public EntryItemViewModel(string id, string title, EntryType entryType, DateTime sortDate,
        string? secondaryText = null, string? priorityText = null, string? tagText = null, string? timeText = null, string? fixedStatusText = null, bool isOutboundMessage = false, string? securityLevelColorHex = null, bool isAlert = false)
    {
        Id = id;
        Title = title;
        EntryType = entryType;
        SortDate = sortDate;
        SecondaryText = secondaryText;
        PriorityText = priorityText;
        TagText = tagText;
        TimeText = timeText;
        FixedStatusText = fixedStatusText;
        IsOutboundMessage = isOutboundMessage;
        SecurityLevelColorHex = securityLevelColorHex;
        IsAlert = isAlert;
    }

    [ObservableProperty] private bool isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusColorHex))]
    private DestinationStatus? overallStatus;

    /// <summary>Gets the unique identifier for this entry (message ID or LiteDB object-id string).</summary>
    public string Id { get; }
    /// <summary>Gets the primary display title for this entry.</summary>
    public string Title { get; }
    /// <summary>Gets an optional secondary line of text shown below the title.</summary>
    public string? SecondaryText { get; }
    /// <summary>Gets an optional priority label shown below <see cref="SecondaryText"/>; see <see cref="Control.IEngineController"/>.</summary>
    public string? PriorityText { get; }
    /// <summary>Gets an optional tag label shown next to <see cref="PriorityText"/>; see <see cref="Control.IEngineController"/>.</summary>
    public string? TagText { get; }
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

    /// <summary>
    /// For <see cref="Data.EntryType.Message"/> entries, the hex color of the message's security level (see
    /// <see cref="Control.IEngineController.SecurityLevels"/>), or <see langword="null"/> when it has none recognized -
    /// no security levels configured, or a level name no longer among them. Renders as a colored banner atop the row.
    /// </summary>
    public string? SecurityLevelColorHex { get; }

    /// <summary>
    /// Gets a value indicating whether this entry is flagged as an alert (see <see cref="Control.IEngineController.GetIsAlert"/>
    /// for messages, or <see cref="Data.Entities.DraftEntity.IsAlert"/> for drafts). <see langword="false"/> for notes
    /// and activity log entries, which have no alert flag. Drives <see cref="TitleColorHex"/> and
    /// <see cref="SecondaryTextColorHex"/>.
    /// </summary>
    public bool IsAlert { get; }

    /// <summary>
    /// Gets the hex color for <see cref="Title"/>: the default light gray, except red when <see cref="IsAlert"/>
    /// and there is no <see cref="SecondaryText"/> - for entry types with no secondary line (drafts, notes),
    /// <see cref="Title"/> itself is the first line of the body, so it takes the alert coloring that would otherwise go
    /// to <see cref="SecondaryTextColorHex"/>. For messages, where <see cref="Title"/> is the sender/destination
    /// rather than the first line of the body, it never turns red.
    /// </summary>
    public string TitleColorHex => IsAlert && string.IsNullOrEmpty(SecondaryText) ? "#E06C75" : "#CCCCCC";

    /// <summary>
    /// Gets the hex color for <see cref="SecondaryText"/>: the default light gray, except red when
    /// <see cref="IsAlert"/> and <see cref="SecondaryText"/> is set - for messages, <see cref="SecondaryText"/>
    /// is the first line of the body, so only it takes the alert coloring rather than the sender/destination in <see cref="Title"/>.
    /// </summary>
    public string SecondaryTextColorHex => IsAlert && !string.IsNullOrEmpty(SecondaryText) ? "#E06C75" : "#CCCCCC";

    /// <summary>Gets the status text to display, derived from the overall delivery status or the fixed status text.</summary>
    public string? StatusText => OverallStatus?.ToString().ToUpperInvariant() ?? FixedStatusText;

    /// <summary>Gets the hex color string for the status text based on delivery outcome.</summary>
    public string StatusColorHex => OverallStatus switch
    {
        DestinationStatus.Failed => "#E06C75",
        DestinationStatus.Read or DestinationStatus.Received => "#98C379",
        _ => "#858585"
    };
}
