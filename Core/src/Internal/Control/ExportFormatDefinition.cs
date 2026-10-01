namespace BlueHeighliner.Comlink.Control;

/// <summary>A custom export format added via <see cref="IEngineBuilder.ExportFormat(string, Func{object, Stream, CancellationToken, Task})"/>.</summary>
internal sealed record ExportFormatDefinition
{
    /// <summary>Display name shown for this format in the client's export screen.</summary>
    public required string Name { get; init; }
    /// <summary>Writes one entry - a <see cref="Services.MessageExportData"/>, <see cref="Services.DraftExportData"/>, <see cref="Services.NoteExportData"/>, or <see cref="Services.ActivityLogExportData"/> - to a stream.</summary>
    public required Func<object, Stream, CancellationToken, Task> Serialize { get; init; }
    /// <summary>Restricts which root folder types this format accepts, or <see langword="null"/> to accept every type.</summary>
    public Func<FolderType, bool>? AllowedTypes { get; init; }
}
