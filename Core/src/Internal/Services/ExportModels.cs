namespace BlueHeighliner.Comlink;

/// <summary>Identifies a single entry to include in an export, mirroring the identity fields of <see cref="EntryItemViewModel"/>.</summary>
internal sealed record ExportEntryRef
{
    /// <summary>Application-level message identifier or LiteDB object-id string, per <see cref="EntryType"/>.</summary>
    public required string Id { get; init; }
    /// <summary>The kind of entry this reference identifies.</summary>
    public required EntryType EntryType { get; init; }
    /// <summary>For <see cref="EntryType.Message"/> entries, disambiguates the Outbox (sent) record from the Inbox (received) record. See <see cref="EntryItemViewModel.IsOutboundMessage"/>.</summary>
    public bool IsOutboundMessage { get; init; }
}
