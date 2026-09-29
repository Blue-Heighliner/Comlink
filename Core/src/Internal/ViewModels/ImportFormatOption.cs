namespace BlueHeighliner.Comlink.ViewModels;

/// <summary>A selectable import format in the import screen's format picker, including the leading built-in package option.</summary>
internal sealed record ImportFormatOption
{
    /// <summary>Gets the display label shown in the picker.</summary>
    public required string Label { get; init; }
    /// <summary>Gets the custom format to read with, or <see langword="null"/> for the built-in package format.</summary>
    public ImportFormatDefinition? Format { get; init; }
}
