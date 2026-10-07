namespace BlueHeighliner.Comlink;

/// <summary>A choice in a draft's message aspect picker: one of the configured message aspects, or none.</summary>
internal sealed record MessageAspectOption
{
    /// <summary>Gets the label shown in the picker.</summary>
    public required string Label { get; init; }

    /// <summary>Gets the message aspect this option sets, or <see langword="null"/> for the option that sets none.</summary>
    public MessageAspect? Aspect { get; init; }
}
