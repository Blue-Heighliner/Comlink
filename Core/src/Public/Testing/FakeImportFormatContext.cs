namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="IImportFormatContext{TPriority, TLevel}"/> a custom import format's reader is handed, to test a reader by what it added. Every addition is recorded and finishes at once.</summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
public sealed class FakeImportFormatContext<TPriority, TLevel> : IImportFormatContext<TPriority, TLevel> where TPriority : struct, Enum where TLevel : struct, Enum
{
    /// <summary>Gets every message the reader added, in order.</summary>
    public List<MessageExportData> Messages { get; } = [];

    /// <summary>Gets every draft the reader added, in order.</summary>
    public List<DraftExportData> Drafts { get; } = [];

    /// <summary>Gets every note the reader added, in order.</summary>
    public List<NoteExportData> Notes { get; } = [];

    /// <summary>Gets every staged send the reader added, in order.</summary>
    public List<StagedSendData<TPriority, TLevel>> StagedSends { get; } = [];

    /// <inheritdoc />
    public Task AddMessage(MessageExportData message)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task AddDraft(DraftExportData draft)
    {
        Drafts.Add(draft);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task AddNote(NoteExportData note)
    {
        Notes.Add(note);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void AddStagedSend(StagedSendData<TPriority, TLevel> send) => StagedSends.Add(send);
}
