namespace BlueHeighliner.Comlink;

/// <summary>A single message priority level: a name paired with its send priority.</summary>
internal sealed record MessagePriorityOption
{
    /// <summary>Gets the display name shown to the user (e.g. in the draft editor's priority picker).</summary>
    public required string Name { get; init; }
    /// <summary>Gets the send priority of the level, its position among the levels (larger values are sent first, see <c>Docs/Components/Peer.md</c>).</summary>
    public required int Value { get; init; }
    /// <summary>Gets whether the GUI offers this priority to a user composing a message; code may use any priority.</summary>
    public PriorityMode Mode { get; init; } = PriorityMode.User;
    /// <summary>Gets the enum member this level was declared from, which is how handlers and blocked combinations name it.</summary>
    public required Enum Key { get; init; }
}
