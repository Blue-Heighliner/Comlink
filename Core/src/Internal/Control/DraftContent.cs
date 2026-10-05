namespace BlueHeighliner.Comlink;

/// <summary>The engine's untyped statement of a draft's current aspects, which a <see cref="IDraftFrameHandler"/> turns into the host's typed <see cref="DraftState{TPriority, TLevel}"/>.</summary>
internal sealed record DraftContent
{
    /// <summary>Gets the draft's tag, or an empty string for none.</summary>
    public required string Tag { get; init; }

    /// <summary>Gets the priority level the draft is set to be sent at, a member of the host's priority enum.</summary>
    public required Enum Priority { get; init; }

    /// <summary>Gets the name of the security level the draft is set to be sent at, or an empty string for none.</summary>
    public required string SecurityLevel { get; init; }

    /// <summary>Gets whether the draft is set to be sent as an alert.</summary>
    public required bool IsAlert { get; init; }

    /// <summary>Gets the draft's recipients so far.</summary>
    public required IReadOnlyList<AddressRequest> Addresses { get; init; }

    /// <summary>Gets how many characters wide a line is set to be, or <see langword="null"/> for no limit.</summary>
    public required int? LineWidth { get; init; }
}
