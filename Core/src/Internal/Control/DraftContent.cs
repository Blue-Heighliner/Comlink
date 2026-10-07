namespace BlueHeighliner.Comlink;

/// <summary>The engine's untyped statement of a draft's current aspects, which a <see cref="IDraftFrameHandler"/> turns into the host's typed <see cref="DraftState{TPriority, TLevel, TAspect}"/>.</summary>
internal sealed record DraftContent
{
    /// <summary>Gets the draft's tag, or an empty string for none.</summary>
    public required string Tag { get; init; }

    /// <summary>Gets the priority level the draft is set to be sent at, a member of the host's priority enum.</summary>
    public required Enum Priority { get; init; }

    /// <summary>Gets the name of the message level the draft is set to be sent at, or an empty string for none.</summary>
    public required string MessageLevel { get; init; }

    /// <summary>Gets the name of the message aspect the draft is set to be sent with, or an empty string for none.</summary>
    public string MessageAspect { get; init; } = string.Empty;

    /// <summary>Gets whether the draft is set to be sent as an alert.</summary>
    public required bool IsAlert { get; init; }

    /// <summary>Gets the draft's recipients so far.</summary>
    public required IReadOnlyList<AddressRequest> Addresses { get; init; }

    /// <summary>Gets how many characters wide a line is set to be, or <see langword="null"/> for no limit.</summary>
    public required int? LineWidth { get; init; }
}
