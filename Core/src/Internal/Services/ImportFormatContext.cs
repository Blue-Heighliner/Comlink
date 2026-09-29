namespace BlueHeighliner.Comlink.Services;

/// <summary>
/// Handed by <see cref="ImportService.Import"/> to a custom import format's reader for the lifetime of a single
/// call, applying <see cref="ImportService"/>'s own message/draft/note insert and conflict rules to whatever the
/// reader adds, and accumulating the counts and staged sends returned in the resulting <see cref="ImportSummary"/>.
/// </summary>
internal sealed class ImportFormatContext : IImportFormatContext
{
    /// <summary>Initializes a new <see cref="ImportFormatContext"/> bound to one <see cref="ImportService.Import"/> call.</summary>
    /// <param name="applyMessage">Inserts a message, skipping a duplicate; returns whether it was inserted.</param>
    /// <param name="applyDraft">Inserts or, on a name conflict, resolves and applies a draft.</param>
    /// <param name="applyNote">Inserts or, on a name conflict, resolves and applies a note.</param>
    /// <param name="resolveConflict">Invoked once per unresolved draft/note name conflict to obtain the user's choice.</param>
    public ImportFormatContext(
        Func<MessageExportData, Task<bool>> applyMessage,
        Func<DraftExportData, Func<ImportConflict, Task<DraftNoteConflictResolution>>, Func<bool>, Action<bool>, Task<(bool Imported, bool Overwritten)>> applyDraft,
        Func<NoteExportData, Func<ImportConflict, Task<DraftNoteConflictResolution>>, Func<bool>, Action<bool>, Task<(bool Imported, bool Overwritten)>> applyNote,
        Func<ImportConflict, Task<DraftNoteConflictResolution>> resolveConflict)
    {
        this.applyMessage = applyMessage;
        this.applyDraft = applyDraft;
        this.applyNote = applyNote;
        this.resolveConflict = resolveConflict;
    }

    private readonly Func<MessageExportData, Task<bool>> applyMessage;
    private readonly Func<DraftExportData, Func<ImportConflict, Task<DraftNoteConflictResolution>>, Func<bool>, Action<bool>, Task<(bool Imported, bool Overwritten)>> applyDraft;
    private readonly Func<NoteExportData, Func<ImportConflict, Task<DraftNoteConflictResolution>>, Func<bool>, Action<bool>, Task<(bool Imported, bool Overwritten)>> applyNote;
    private readonly Func<ImportConflict, Task<DraftNoteConflictResolution>> resolveConflict;
    private readonly List<StagedSendData> stagedSends = [];
    private int imported;
    private int skipped;
    private int overwritten;
    private bool overwriteAll;

    /// <inheritdoc />
    public async Task AddMessage(MessageExportData message)
    {
        if (await applyMessage(message)) { imported++; }
        else { skipped++; }
    }

    /// <inheritdoc />
    public async Task AddDraft(DraftExportData draft)
    {
        (bool wasImported, bool wasOverwritten) = await applyDraft(draft, resolveConflict, () => overwriteAll, v => overwriteAll = v);
        if (wasOverwritten) { overwritten++; }
        else if (wasImported) { imported++; }
        else { skipped++; }
    }

    /// <inheritdoc />
    public async Task AddNote(NoteExportData note)
    {
        (bool wasImported, bool wasOverwritten) = await applyNote(note, resolveConflict, () => overwriteAll, v => overwriteAll = v);
        if (wasOverwritten) { overwritten++; }
        else if (wasImported) { imported++; }
        else { skipped++; }
    }

    /// <inheritdoc />
    public void AddStagedSend(StagedSendData send) => stagedSends.Add(send);

    /// <summary>Builds the <see cref="ImportSummary"/> for everything added through this context so far.</summary>
    public ImportSummary BuildSummary() => new() { Imported = imported, Skipped = skipped, Overwritten = overwritten, StagedSends = stagedSends };
}
