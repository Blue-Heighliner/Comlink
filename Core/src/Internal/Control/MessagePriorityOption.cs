namespace BlueHeighliner.Comlink;

/// <summary>A single selectable message priority level: a display name paired with its wire priority number.</summary>
internal sealed record MessagePriorityOption
{
    /// <summary>Gets the display name shown to the user (e.g. in the draft editor's priority picker).</summary>
    public required string Name { get; init; }
    /// <summary>
    /// Gets the priority number stored in the message's priority field (see <see cref="IFrameBuilder{TFrame}"/>) and used verbatim as
    /// the MSMT send priority (larger values are sent first — see <c>Docs/Components/Peer.md</c>).
    /// </summary>
    public required int Value { get; init; }
    /// <summary>Gets who may assign this priority; only <see cref="PriorityMode.User"/> priorities are offered to a user composing a message.</summary>
    public PriorityMode Mode { get; init; } = PriorityMode.User;
    /// <summary>Gets the enum member this level was declared from, which is how handlers and blocked combinations name it, or <see langword="null"/> for the default level.</summary>
    public Enum? Key { get; init; }
}

/// <summary>Extension helpers for looking up display information from a set of <see cref="MessagePriorityOption"/> values.</summary>
internal static class MessagePriorityOptionExtensions
{
    /// <summary>
    /// Returns the display <see cref="MessagePriorityOption.Name"/> matching <paramref name="value"/>, or the
    /// plain numeric value as a string if no option in <paramref name="priorities"/> matches — e.g. after a
    /// host changes its <see cref="IEngineBuilder.Priorities"/> and an older stored value
    /// no longer has a corresponding option.
    /// </summary>
    public static string GetLabel(this IReadOnlyList<MessagePriorityOption> priorities, int value)
        => priorities.FirstOrDefault(p => p.Value == value)?.Name ?? value.ToString();
}
