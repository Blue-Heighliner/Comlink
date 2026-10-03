namespace BlueHeighliner.Comlink.Sample;

/// <summary>A plain-text alternative to the built-in JSON export, restricted to messages, drafts, and notes (an activity log's structured entries don't read naturally as prose).</summary>
public sealed class TextExportFormat : IExportFormat
{
    /// <inheritdoc />
    public string Name { get; } = "Text";

    /// <inheritdoc />
    public bool Accepts(FolderType type) => type is FolderType.Inbox or FolderType.Outbox or FolderType.Drafts or FolderType.Notes;

    /// <inheritdoc />
    public async Task Export(object entry, Stream stream, CancellationToken cancellation)
    {
        string text = entry switch
        {
            MessageExportData message => $"{(message.IsOutbound ? "To" : "From")}: {string.Join(", ", message.Addresses.Select(a => a.UserName))}\n\n{message.Body}\n",
            DraftExportData draft => $"{draft.Body}\n",
            NoteExportData note => $"{note.Body}\n",
            _ => throw new ArgumentException($"Unsupported entry type '{entry.GetType()}' for the Text export format.", nameof(entry))
        };
        await using StreamWriter writer = new(stream, leaveOpen: true);
        await writer.WriteAsync(text);
    }
}
