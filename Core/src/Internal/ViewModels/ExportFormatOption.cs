namespace BlueHeighliner.Comlink;

/// <summary>A selectable export format in the export screen's format picker, including the leading built-in JSON option.</summary>
internal sealed record ExportFormatOption
{
    /// <summary>Gets the display label shown in the picker.</summary>
    public required string Label { get; init; }
    /// <summary>Gets the custom format to export with, or <see langword="null"/> for the built-in JSON format.</summary>
    public ExportFormatDefinition? Format { get; init; }
}
