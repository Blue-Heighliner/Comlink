namespace BlueHeighliner.Comlink;

/// <summary>A selectable priority level in the entry list's filter panel, including the leading "Any" (no filter) option.</summary>
internal sealed record PriorityFilterOption
{
    /// <summary>Gets the display label shown in the picker.</summary>
    public required string Label { get; init; }
    /// <summary>Gets the priority level to filter on, or <see langword="null"/> for "Any" (no filter).</summary>
    public Enum? Value { get; init; }
}
