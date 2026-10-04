namespace BlueHeighliner.Comlink;

/// <summary>
/// Handed to a custom import format's reader (see <see cref="IImportsBuilder{TFrame, TPacket, TPriority, TLevel}.Format{TFormat}"/>) to turn what it
/// reads from the stream into new entries or staged sends. <see cref="AddMessage"/>/<see cref="AddDraft"/>/
/// <see cref="AddNote"/> apply the same insert/conflict rules a built-in package's own entries go through - a
/// draft/note whose name matches an existing entry prompts the user the same way, sharing that same prompt UI -
/// so a reader only needs to build the DTO, not reimplement matching.
/// </summary>
public interface IImportFormatContext
{
    /// <summary>
    /// Adds a message, restored into the Inbox or Outbox per <see cref="MessageExportData.IsOutbound"/>. Skipped,
    /// with no prompt, if an entry with the same <see cref="MessageExportData.MessageId"/>, direction, and
    /// received date already exists.
    /// </summary>
    Task AddMessage(MessageExportData message);

    /// <summary>
    /// Adds a draft. An existing draft with the same (trimmed) first line of <see cref="DraftExportData.Body"/> prompts the
    /// user to keep, overwrite, or overwrite-all, the same as a conflicting entry from a built-in package.
    /// </summary>
    Task AddDraft(DraftExportData draft);

    /// <summary>
    /// Adds a note. An existing note whose body's first line matches prompts the user to keep, overwrite, or
    /// overwrite-all, the same as a conflicting entry from a built-in package.
    /// </summary>
    Task AddNote(NoteExportData note);

    /// <summary>
    /// Adds a prepared message to the staged send screen, for the user to review and send later - never sent
    /// automatically. Fire-and-forget: only queues the send, so this returns immediately.
    /// </summary>
    void AddStagedSend(StagedSendData send);
}
