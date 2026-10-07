namespace BlueHeighliner.Comlink;

/// <summary>
/// Exported representation of a message entry: what the engine's own built-in JSON export writes, and what a
/// custom export format's serializer (see <see cref="IExportsBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Format{TFormat}"/>) receives for a message.
/// </summary>
public sealed record MessageExportData
{
    /// <summary>Application-level message identifier shared with peers.</summary>
    public required string MessageId { get; init; }
    /// <summary><see langword="true"/> for an Outbox (sent) record, <see langword="false"/> for an Inbox (received) record.</summary>
    public required bool IsOutbound { get; init; }
    /// <summary>User name of the sender.</summary>
    public required string FromUser { get; init; }
    /// <summary>Message body text.</summary>
    public required string Body { get; init; }
    /// <summary>Recipient addresses on this message.</summary>
    public required List<AddressRequest> Addresses { get; init; }
    /// <summary>UTC timestamp when the message was sent.</summary>
    public required DateTime SentAt { get; init; }
    /// <summary>Whether this message was sent as an alert.</summary>
    public required bool IsAlert { get; init; }
    /// <summary>Integer value of the enum member that is the priority level of this message, one of the configured priorities.</summary>
    public required int Priority { get; init; }
    /// <summary>Tag identifying the type of this message; see <see cref="IEngineController.GetTag"/>.</summary>
    public required string Tag { get; init; }
    /// <summary>UTC timestamp when this record was received or created.</summary>
    public required DateTime ReceivedAt { get; init; }
    /// <summary>Inbox-only read status; <see langword="null"/> on Outbox records.</summary>
    public DestinationStatus? ReadStatus { get; init; }
    /// <summary>Per-destination delivery statuses on an Outbox record.</summary>
    public required List<MessageDeliveryStatus> DeliveryStatuses { get; init; }
}

/// <summary>Delivery outcome for a single recipient user, as carried by <see cref="MessageExportData.DeliveryStatuses"/>.</summary>
public sealed record MessageDeliveryStatus
{
    /// <summary>Name of the destination user.</summary>
    public required string UserName { get; init; }
    /// <summary>Delivery status for this user at the time of export.</summary>
    public required DestinationStatus Status { get; init; }
    /// <summary>Names of the groups in the message's address list that contained this user.</summary>
    public List<string> AddressedVia { get; init; } = [];
}

/// <summary>
/// Exported representation of a draft entry: what the engine's own built-in JSON export writes, and what a
/// custom export format's serializer (see <see cref="IExportsBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Format{TFormat}"/>) receives for a draft.
/// </summary>
public sealed record DraftExportData
{
    /// <summary>LiteDB object-id string for this draft.</summary>
    public required string Id { get; init; }
    /// <summary>The name the user gave the draft, or <see langword="null"/> when it is named by the first line of its body.</summary>
    public string? Name { get; init; }
    /// <summary>Plain-text body of the draft.</summary>
    public required string Body { get; init; }
    /// <summary>The draft body with its fill-ins, or <see langword="null"/> in a package written before fill-ins were exported, in which case only <see cref="Body"/> is restored.</summary>
    public string? BodySegmentsJson { get; init; }
    /// <summary>Recipient addresses on this draft.</summary>
    public required List<AddressRequest> Addresses { get; init; }
    /// <summary>Whether this draft has been sent.</summary>
    public required bool IsSent { get; init; }
    /// <summary>Whether this draft is marked to send as an alert.</summary>
    public required bool IsAlert { get; init; }
    /// <summary>Integer value of the enum member that is the priority level this draft should be sent at.</summary>
    public required int Priority { get; init; }
    /// <summary>Tag identifying the type of this draft; see <see cref="IEngineController.GetTag"/>.</summary>
    public required string Tag { get; init; }
    /// <summary>Integer value of the message level the draft is set to be sent at, a member of the enum the host stated for its message levels, or <see langword="null"/> for none.</summary>
    public int? MessageLevel { get; init; }
    /// <summary>Integer value of the message aspect the draft is set to be sent with, a member of the enum the host stated for its message aspects, or <see langword="null"/> for none.</summary>
    public int? MessageAspect { get; init; }
    /// <summary>UTC timestamp when the draft was sent, or <see langword="null"/> if not yet sent.</summary>
    public DateTime? SentAt { get; init; }
    /// <summary>UTC timestamp when this draft was first created.</summary>
    public required DateTime CreatedAt { get; init; }
    /// <summary>UTC timestamp of the most recent modification.</summary>
    public required DateTime ModifiedAt { get; init; }
}

/// <summary>
/// Exported representation of a note entry: what the engine's own built-in JSON export writes, and what a
/// custom export format's serializer (see <see cref="IExportsBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Format{TFormat}"/>) receives for a note.
/// </summary>
public sealed record NoteExportData
{
    /// <summary>LiteDB object-id string for this note.</summary>
    public required string Id { get; init; }
    /// <summary>The name the user gave the note, or <see langword="null"/> when it is named by the first line of its body.</summary>
    public string? Name { get; init; }
    /// <summary>Text body of the note.</summary>
    public required string Body { get; init; }
    /// <summary>UTC timestamp when this note was first created.</summary>
    public required DateTime CreatedAt { get; init; }
    /// <summary>UTC timestamp of the most recent modification.</summary>
    public required DateTime ModifiedAt { get; init; }
}

/// <summary>
/// Exported representation of an activity log entry: what the engine's own built-in JSON export writes, and what
/// a custom export format's serializer (see <see cref="IExportsBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Format{TFormat}"/>) receives for an activity log.
/// </summary>
public sealed record ActivityLogExportData
{
    /// <summary>LiteDB object-id string for this activity log.</summary>
    public required string Id { get; init; }
    /// <summary>The UTC calendar date this log covers.</summary>
    public required DateTime Date { get; init; }
    /// <summary>Structured log entries recorded for this day.</summary>
    public required List<ActivityLogEventEntry> EventEntries { get; init; }
}

/// <summary>A single timestamped event within <see cref="ActivityLogExportData.EventEntries"/>.</summary>
public sealed record ActivityLogEventEntry
{
    /// <summary>UTC timestamp when this event was recorded.</summary>
    public required DateTime At { get; init; }
    /// <summary>Human-readable description of the event.</summary>
    public required string Message { get; init; }
    /// <summary>The unique identifier of the kind of event this is, or <c>0</c> for an entry that has none.</summary>
    public int EventId { get; init; }
}
