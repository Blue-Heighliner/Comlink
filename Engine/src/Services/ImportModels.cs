namespace BlueHeighliner.Comlink.Engine.Services;

/// <summary>Describes a single export package (<c>.export.zip</c>) found on a drive, available to import.</summary>
public sealed record ImportPackageInfo
{
    /// <summary>Gets the package's file name, including the <see cref="IExportService.PackageExtension"/> extension.</summary>
    public required string FileName { get; init; }
    /// <summary>Gets the package's absolute path.</summary>
    public required string FullPath { get; init; }
}

/// <summary>
/// A draft or note being imported whose name (subject, or note first line) matches an entry that already
/// exists, requiring the user to choose how to proceed.
/// </summary>
public sealed record ImportConflict
{
    /// <summary>Gets the type of the conflicting entry — always <see cref="Data.EntryType.Draft"/> or <see cref="Data.EntryType.Note"/>.</summary>
    public required EntryType EntryType { get; init; }
    /// <summary>Gets the matching name — the draft's subject, or the note's first line.</summary>
    public required string Name { get; init; }
}

/// <summary>The user's choice for resolving an <see cref="ImportConflict"/>.</summary>
public enum DraftNoteConflictResolution
{
    /// <summary>Keep the existing entry; skip this imported entry.</summary>
    KeepExisting,
    /// <summary>Overwrite the existing entry's content with the imported entry.</summary>
    Overwrite,
    /// <summary>Overwrite this entry, and every remaining conflict in this import, without asking again.</summary>
    OverwriteAll
}

/// <summary>Outcome counts for a completed <see cref="IImportService.Import"/> call.</summary>
public sealed record ImportSummary
{
    /// <summary>Gets the number of entries inserted as new (no conflicting entry existed).</summary>
    public required int Imported { get; init; }
    /// <summary>Gets the number of entries skipped — a duplicate message, or a draft/note conflict resolved as <see cref="DraftNoteConflictResolution.KeepExisting"/>.</summary>
    public required int Skipped { get; init; }
    /// <summary>Gets the number of existing drafts/notes overwritten with imported content.</summary>
    public required int Overwritten { get; init; }
}
