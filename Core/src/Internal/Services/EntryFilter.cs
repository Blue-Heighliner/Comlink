namespace BlueHeighliner.Comlink;

/// <summary>
/// Optional entry-listing filter criteria for <see cref="IEntryService.GetMessages"/>/<see cref="IEntryService.GetDrafts"/>/<see cref="IEntryService.GetNotes"/>.
/// Each criterion left <see langword="null"/> matches every entry; which criteria apply is entry-type-specific -
/// see each method's own documentation for exactly which fields a criterion is checked against.
/// </summary>
internal sealed record EntryFilter
{
    /// <summary>Case-insensitive substring match against fields specific to the entry type. Whitespace-only is treated the same as <see langword="null"/> (no constraint).</summary>
    public string? Search { get; init; }
    /// <summary>
    /// Inclusive lower bound on the entry's own timestamp (received for messages, last modified for drafts and
    /// notes), compared as an exact instant. A caller wanting a whole calendar day rather than a specific moment
    /// combines the chosen date with midnight itself (<c>date.Date</c>) - <see cref="EntryBarViewModel"/> does
    /// this by default when the user picks only a date and no time.
    /// </summary>
    public DateTime? DateFrom { get; init; }
    /// <summary>
    /// Inclusive upper bound on the entry's own timestamp, compared as an exact instant. A caller wanting a whole
    /// calendar day combines the chosen date with its last instant (23:59:59.999) - <see cref="EntryBarViewModel"/>
    /// does this by default when the user picks only a date and no time.
    /// </summary>
    public DateTime? DateTo { get; init; }
    /// <summary>Case-insensitive substring match against a message's sender user name. Messages only (used for the Inbox); ignored for drafts and notes. Whitespace-only is treated the same as <see langword="null"/>.</summary>
    public string? Author { get; init; }
    /// <summary>Case-insensitive substring match against any addressee's user name (To, Cc or External). Messages and drafts (used for the Outbox and Drafts); ignored for notes. Whitespace-only is treated the same as <see langword="null"/>.</summary>
    public string? Destination { get; init; }
    /// <summary>Matches only entries sent/composed at this exact message level name. Messages and drafts; ignored for notes.</summary>
    public string? MessageLevel { get; init; }
    /// <summary>Matches only entries sent/composed at this priority level. Messages and drafts; ignored for notes.</summary>
    public Enum? Priority { get; init; }
    /// <summary>When <see langword="true"/>, matches only messages that are alerts; when <see langword="false"/>, only messages that are not. <see langword="null"/> matches both. Messages only; ignored for drafts and notes.</summary>
    public bool? Alert { get; init; }

    /// <summary>
    /// Gets a value indicating whether every criterion is unset. <see cref="IEntryService"/> skips filtering
    /// entirely in this case and paginates the folder's LiteDB query directly, rather than loading the whole
    /// folder to filter in memory.
    /// </summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Search) && DateFrom is null && DateTo is null && string.IsNullOrWhiteSpace(Author) && string.IsNullOrWhiteSpace(Destination) && MessageLevel is null && Priority is null && Alert is null;
}
