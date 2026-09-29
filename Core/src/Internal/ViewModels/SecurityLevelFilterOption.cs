namespace BlueHeighliner.Comlink.ViewModels;

/// <summary>A selectable security level in the entry list's filter panel, including the leading "Any" (no filter) option.</summary>
internal sealed record SecurityLevelFilterOption
{
    /// <summary>Gets the display label shown in the picker.</summary>
    public required string Label { get; init; }
    /// <summary>Gets the security level name to filter on, or <see langword="null"/> for "Any" (no filter).</summary>
    public string? Name { get; init; }
}
