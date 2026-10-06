namespace BlueHeighliner.Comlink;

/// <summary>Where a stored entry is kept, as found by <see cref="IEntryService.Locate"/>.</summary>
/// <param name="FolderId">The identifier of the folder that holds the entry.</param>
/// <param name="IsAlert">Whether the entry is an alert message; always <see langword="false"/> for anything else.</param>
internal sealed record EntryLocation(string FolderId, bool IsAlert);
