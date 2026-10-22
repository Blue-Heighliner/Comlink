namespace BlueHeighliner.Comlink;

/// <summary>The engine's untyped statement of a draft's current aspects, which a <see cref="IDraftFrameHandler"/> turns into the host's typed <see cref="IDraftContext{TPriority, TLevel, TAspect}"/>.</summary>
internal sealed record DraftContent
{
    /// <summary>Gets the draft's tag, or an empty string for none.</summary>
    public required string Tag { get; init; }

    /// <summary>Gets the priority level the draft is set to be sent at, a member of the host's priority enum.</summary>
    public required Enum Priority { get; init; }

    /// <summary>Gets the name of the level the draft is set to be sent at, or an empty string for none.</summary>
    public required Enum? Level { get; init; }

    /// <summary>Gets the name of the aspect the draft is set to be sent with, or an empty string for none.</summary>
    public Enum? Aspect { get; init; }

    /// <summary>Gets the draft's body text as it currently is.</summary>
    public required string Body { get; init; }

    /// <summary>Gets the draft's recipients so far.</summary>
    public required IReadOnlyList<AddressRequest> Addresses { get; init; }

    /// <summary>Gets how many characters wide a line is set to be, or <see langword="null"/> for no limit.</summary>
    public required int? LineWidth { get; init; }
}
