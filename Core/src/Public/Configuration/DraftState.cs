namespace BlueHeighliner.Comlink;

/// <summary>What an <see cref="IDraftHandler{TPriority, TLevel}"/> is told about a draft when it is asked for the draft's header: the draft's aspects as they currently are, which change as the user edits it.</summary>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the security levels.</typeparam>
public sealed record DraftState<TPriority, TLevel> where TPriority : struct, Enum where TLevel : struct, Enum
{
    /// <summary>Gets the draft's tag, or an empty string for none.</summary>
    public required string Tag { get; init; }

    /// <summary>Gets the priority level the draft is set to be sent at.</summary>
    public required TPriority Priority { get; init; }

    /// <summary>Gets the security level the draft is set to be sent at, or <see langword="null"/> for none.</summary>
    public required TLevel? SecurityLevel { get; init; }

    /// <summary>Gets whether the draft is set to be sent as an alert.</summary>
    public required bool IsAlert { get; init; }

    /// <summary>Gets the draft's recipients so far.</summary>
    public required IReadOnlyList<AddressRequest> Addresses { get; init; }

    /// <summary>Gets how many monospace characters wide a line of the draft is set to be, or <see langword="null"/> for no limit.</summary>
    public required int? LineWidth { get; init; }
}
