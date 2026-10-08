namespace BlueHeighliner.Comlink;

/// <summary>Describes a single export package (<c>.export.zip</c>) found on a drive, available to import.</summary>
internal sealed record ImportPackageInfo
{
    /// <summary>Gets the package's file name, including the <see cref="IExportService.PackageExtension"/> extension.</summary>
    public required string FileName { get; init; }
    /// <summary>Gets the package's absolute path.</summary>
    public required string FullPath { get; init; }
}

/// <summary>
/// A draft or note being imported whose name (first line of the body) matches an entry that already
/// exists, requiring the user to choose how to proceed.
/// </summary>
internal sealed record ImportConflict
{
    /// <summary>Gets the type of the conflicting entry — always <see cref="EntryType.Draft"/> or <see cref="EntryType.Note"/>.</summary>
    public required EntryType EntryType { get; init; }
    /// <summary>Gets the matching name — the first line of the draft's or note's body.</summary>
    public required string Name { get; init; }
}

/// <summary>The user's choice for resolving an <see cref="ImportConflict"/>.</summary>
internal enum DraftNoteConflictResolution
{
    /// <summary>Keep the existing entry; skip this imported entry.</summary>
    KeepExisting,
    /// <summary>Overwrite the existing entry's content with the imported entry.</summary>
    Overwrite,
    /// <summary>Overwrite this entry, and every remaining conflict in this import, without asking again.</summary>
    OverwriteAll
}

/// <summary>Outcome counts for a completed <see cref="IImportService.Import"/> call.</summary>
internal sealed record ImportSummary
{
    /// <summary>Gets the number of entries inserted as new (no conflicting entry existed).</summary>
    public required int Imported { get; init; }
    /// <summary>Gets the number of entries skipped — a duplicate message, or a draft/note conflict resolved as <see cref="DraftNoteConflictResolution.KeepExisting"/>.</summary>
    public required int Skipped { get; init; }
    /// <summary>Gets the number of existing drafts/notes overwritten with imported content.</summary>
    public required int Overwritten { get; init; }
    /// <summary>Gets the staged sends a custom format's reader added via <see cref="IImportContext.AddStagedSend"/>; always empty for the built-in package format.</summary>
    public IReadOnlyList<StagedSendData> StagedSends { get; init; } = [];
}
