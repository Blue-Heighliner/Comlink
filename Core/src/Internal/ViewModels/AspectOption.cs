namespace BlueHeighliner.Comlink;

/// <summary>A choice in a draft's aspect picker: one of the configured aspects, or none.</summary>
internal sealed record AspectOption
{
    /// <summary>Gets the label shown in the picker.</summary>
    public required string Label { get; init; }

    /// <summary>Gets the aspect this option sets, or <see langword="null"/> for the option that sets none.</summary>
    public AspectDefinition? Aspect { get; init; }
}
