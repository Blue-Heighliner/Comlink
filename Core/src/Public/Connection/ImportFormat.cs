namespace BlueHeighliner.Comlink;

/// <summary>
/// A custom import format, shown as an option alongside the built-in package format in the client's import screen. State one with
/// <see cref="IImportsBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Format{TFormat}"/>. Its files are found on the source drive by an extension derived from <see cref="Name"/> the same way an
/// <see cref="IExportFormat"/>'s is. Unlike the built-in format, this is the reader's own file layout, not a zip archive of typed entries.
/// </summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
public interface IImportFormat<TPriority, TLevel> where TPriority : struct, Enum where TLevel : struct, Enum
{
    /// <summary>Gets the display name shown for this format in the import screen. A later format of the same name (case-insensitive) replaces an earlier one in place.</summary>
    string Name { get; }

    /// <summary>Gets whether this format's staged sends, added via <see cref="IImportFormatContext{TPriority, TLevel}.AddStagedSend"/>, are all sent at once or one at a time, once the user presses the staged send screen's final send button. One at a time unless overridden.</summary>
    StagedSendMode StagedSendMode => StagedSendMode.Sequential;

    /// <summary>Gets, while <see cref="StagedSendMode"/> is <see cref="StagedSendMode.Sequential"/>, an optional pause between each send. <see langword="null"/> sends the next immediately.</summary>
    TimeSpan? StagedSendDelay => null;

    /// <summary>Reads one whole file the user chose, turning what it reads into new messages, drafts, notes, and staged sends through <paramref name="context"/>.</summary>
    /// <param name="stream">The file's contents.</param>
    /// <param name="context">Adds what is read to the app.</param>
    /// <param name="cancellation">Cancels the import.</param>
    Task Import(Stream stream, IImportFormatContext<TPriority, TLevel> context, CancellationToken cancellation);
}
