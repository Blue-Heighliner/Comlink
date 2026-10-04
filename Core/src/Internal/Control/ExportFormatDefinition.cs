namespace BlueHeighliner.Comlink;

/// <summary>A custom export format added via <see cref="IExportsBuilder{TFrame, TPacket, TPriority, TLevel}.Format{TFormat}"/>.</summary>
internal sealed record ExportFormatDefinition
{
    /// <summary>Display name shown for this format in the client's export screen.</summary>
    public required string Name { get; init; }
    /// <summary>Writes one entry - a <see cref="MessageExportData"/>, <see cref="DraftExportData"/>, <see cref="NoteExportData"/>, or <see cref="ActivityLogExportData"/> - to a stream.</summary>
    public required Func<object, Stream, CancellationToken, Task> Serialize { get; init; }
    /// <summary>Restricts which root folder types this format accepts, or <see langword="null"/> to accept every type.</summary>
    public Func<FolderType, bool>? AllowedTypes { get; init; }
}
