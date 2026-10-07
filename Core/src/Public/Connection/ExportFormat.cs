namespace BlueHeighliner.Comlink;

/// <summary>
/// A custom export format, shown as an option alongside the built-in JSON format in the client's export screen. State one with
/// <see cref="IExportsBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Format{TFormat}"/>. Each entry in the export zip gets a file extension derived from <see cref="Name"/>
/// (lowercased, stripped to letters and digits). A package written with a custom format is one-way: only the built-in JSON format can be imported again.
/// </summary>
public interface IExportFormat
{
    /// <summary>Gets the display name shown for this format in the export screen. A later format of the same name (case-insensitive) replaces an earlier one in place.</summary>
    string Name { get; }

    /// <summary>
    /// Returns whether this format accepts entries from <paramref name="type"/>; an entry outside them is left out of an export using this format instead of being passed to
    /// <see cref="Export"/>. Every root folder type is accepted unless this is overridden.
    /// </summary>
    /// <param name="type">The root folder type an entry came from.</param>
    bool Accepts(FolderType type) => true;

    /// <summary>
    /// Writes one entry - a <see cref="MessageExportData"/>, <see cref="DraftExportData"/>, <see cref="NoteExportData"/>, or <see cref="ActivityLogExportData"/>,
    /// depending on which root folder type it came from - to a stream.
    /// </summary>
    /// <param name="entry">The entry to write.</param>
    /// <param name="stream">Where to write it.</param>
    /// <param name="cancellation">Cancels the export.</param>
    Task Export(object entry, Stream stream, CancellationToken cancellation);
}
