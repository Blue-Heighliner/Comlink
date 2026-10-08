namespace BlueHeighliner.Comlink;

/// <summary>What an import format adds entries and staged sends through, with staged sends as the engine holds them.</summary>
internal interface IImportContext
{
    /// <summary>Applies a message the way a package's own messages are applied.</summary>
    /// <param name="message">The message.</param>
    Task AddMessage(MessageExportData message);

    /// <summary>Applies a draft the way a package's own drafts are applied.</summary>
    /// <param name="draft">The draft.</param>
    Task AddDraft(DraftExportData draft);

    /// <summary>Applies a note the way a package's own notes are applied.</summary>
    /// <param name="note">The note.</param>
    Task AddNote(NoteExportData note);

    /// <summary>Adds a message for the user to review and send.</summary>
    /// <param name="send">The message.</param>
    void AddStagedSend(StagedSendData send);
}

/// <summary>The engine's view of a host's import format, with staged sends as the engine holds them.</summary>
internal interface IImportSource
{
    /// <summary>Gets the display name of the format.</summary>
    string Name { get; }

    /// <summary>Gets whether staged sends are sent all at once or one at a time.</summary>
    StagedSendMode StagedSendMode { get; }

    /// <summary>Gets the optional pause between sequential staged sends.</summary>
    TimeSpan? StagedSendDelay { get; }

    /// <summary>Reads one whole file the user chose.</summary>
    /// <param name="stream">The file's contents.</param>
    /// <param name="context">Adds what is read to the app.</param>
    /// <param name="cancellation">Cancels the import.</param>
    Task Import(Stream stream, IImportContext context, CancellationToken cancellation);
}

/// <summary>Presents a host's <see cref="IImportFormat{TPriority, TLevel}"/> as an <see cref="IImportSource"/>.</summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <param name="format">The host's format.</param>
internal sealed class ImportSource<TPriority, TLevel>(IImportFormat<TPriority, TLevel> format) : IImportSource where TPriority : struct, Enum where TLevel : struct, Enum
{
    /// <inheritdoc />
    public string Name => format.Name;

    /// <inheritdoc />
    public StagedSendMode StagedSendMode => format.StagedSendMode;

    /// <inheritdoc />
    public TimeSpan? StagedSendDelay => format.StagedSendDelay;

    /// <inheritdoc />
    public Task Import(Stream stream, IImportContext context, CancellationToken cancellation) => format.Import(stream, new Context(context), cancellation);

    private sealed class Context(IImportContext context) : IImportFormatContext<TPriority, TLevel>
    {
        public Task AddMessage(MessageExportData message) => context.AddMessage(message);

        public Task AddDraft(DraftExportData draft) => context.AddDraft(draft);

        public Task AddNote(NoteExportData note) => context.AddNote(note);

        public void AddStagedSend(StagedSendData<TPriority, TLevel> send)
            => context.AddStagedSend(new StagedSendData { Body = send.Body, Addresses = send.Addresses, Priority = send.Priority, Tag = send.Tag, MessageLevel = send.MessageLevel });
    }
}
