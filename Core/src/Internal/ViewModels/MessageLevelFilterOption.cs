namespace BlueHeighliner.Comlink;

/// <summary>A selectable message level in the entry list's filter panel, including the leading "Any" (no filter) option.</summary>
internal sealed record MessageLevelFilterOption
{
    /// <summary>Gets the display label shown in the picker.</summary>
    public required string Label { get; init; }
    /// <summary>Gets the message level name to filter on, or <see langword="null"/> for "Any" (no filter).</summary>
    public string? Name { get; init; }
}
