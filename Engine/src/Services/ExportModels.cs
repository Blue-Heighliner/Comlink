namespace BlueHeighliner.Comlink.Engine.Services;

/// <summary>Identifies a single entry to include in an export, mirroring the identity fields of <see cref="ViewModels.EntryItemViewModel"/>.</summary>
public sealed record ExportEntryRef
{
    /// <summary>Application-level message identifier or LiteDB object-id string, per <see cref="EntryType"/>.</summary>
    public required string Id { get; init; }
    /// <summary>The kind of entry this reference identifies.</summary>
    public required EntryType EntryType { get; init; }
    /// <summary>For <see cref="Data.EntryType.Message"/> entries, disambiguates the Outbox (sent) record from the Inbox (received) record. See <see cref="ViewModels.EntryItemViewModel.IsOutboundMessage"/>.</summary>
    public bool IsOutboundMessage { get; init; }
}

/// <summary>Clean JSON export representation of a message entry.</summary>
public sealed record MessageExportData
{
    /// <summary>Application-level message identifier shared with peers.</summary>
    public required string MessageId { get; init; }
    /// <summary><see langword="true"/> for an Outbox (sent) record, <see langword="false"/> for an Inbox (received) record.</summary>
    public required bool IsOutbound { get; init; }
    /// <summary>User name of the sender.</summary>
    public required string FromUser { get; init; }
    /// <summary>Message subject line.</summary>
    public required string Subject { get; init; }
    /// <summary>Message body text.</summary>
    public required string Body { get; init; }
    /// <summary>Recipient addresses on this message.</summary>
    public required List<AddressData> Addresses { get; init; }
    /// <summary>UTC timestamp when the message was sent.</summary>
    public required DateTime SentAt { get; init; }
    /// <summary>Whether this message was sent as an alert.</summary>
    public required bool IsAlert { get; init; }
    /// <summary>Priority number of this message; see <see cref="Control.IMessageFormat.GetPriority"/>.</summary>
    public required int Priority { get; init; }
    /// <summary>Tag identifying the type of this message; see <see cref="Control.IMessageFormat.GetTag"/>.</summary>
    public required string Tag { get; init; }
    /// <summary>UTC timestamp when this record was received or created.</summary>
    public required DateTime ReceivedAt { get; init; }
    /// <summary>Inbox-only read status; <see langword="null"/> on Outbox records.</summary>
    public DestinationStatus? ReadStatus { get; init; }
    /// <summary>Per-destination delivery statuses on an Outbox record.</summary>
    public required List<DeliveryStatus> DeliveryStatuses { get; init; }
}

/// <summary>Clean JSON export representation of a draft entry.</summary>
public sealed record DraftExportData
{
    /// <summary>LiteDB object-id string for this draft.</summary>
    public required string Id { get; init; }
    /// <summary>Subject line of the draft.</summary>
    public required string Subject { get; init; }
    /// <summary>Plain-text body of the draft.</summary>
    public required string Body { get; init; }
    /// <summary>Recipient addresses on this draft.</summary>
    public required List<AddressData> Addresses { get; init; }
    /// <summary>Whether this draft has been sent.</summary>
    public required bool IsSent { get; init; }
    /// <summary>Whether this draft is marked to send as an alert.</summary>
    public required bool IsAlert { get; init; }
    /// <summary>Priority number this draft should be sent at; see <see cref="Control.IMessageFormat.GetPriority"/>.</summary>
    public required int Priority { get; init; }
    /// <summary>Tag identifying the type of this draft; see <see cref="Control.IMessageFormat.GetTag"/>.</summary>
    public required string Tag { get; init; }
    /// <summary>UTC timestamp when the draft was sent, or <see langword="null"/> if not yet sent.</summary>
    public DateTime? SentAt { get; init; }
    /// <summary>UTC timestamp when this draft was first created.</summary>
    public required DateTime CreatedAt { get; init; }
    /// <summary>UTC timestamp of the most recent modification.</summary>
    public required DateTime ModifiedAt { get; init; }
}

/// <summary>Clean JSON export representation of a note entry.</summary>
public sealed record NoteExportData
{
    /// <summary>LiteDB object-id string for this note.</summary>
    public required string Id { get; init; }
    /// <summary>Text body of the note.</summary>
    public required string Body { get; init; }
    /// <summary>UTC timestamp when this note was first created.</summary>
    public required DateTime CreatedAt { get; init; }
    /// <summary>UTC timestamp of the most recent modification.</summary>
    public required DateTime ModifiedAt { get; init; }
}

/// <summary>Clean JSON export representation of an activity log entry.</summary>
public sealed record ActivityLogExportData
{
    /// <summary>LiteDB object-id string for this activity log.</summary>
    public required string Id { get; init; }
    /// <summary>The UTC calendar date this log covers.</summary>
    public required DateTime Date { get; init; }
    /// <summary>Structured log entries recorded for this day.</summary>
    public required List<ActivityLogEntry> EventEntries { get; init; }
}
