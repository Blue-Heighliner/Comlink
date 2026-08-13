namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>A single selectable message priority level: a display name paired with its wire priority number.</summary>
public sealed record MessagePriorityOption
{
    /// <summary>Gets the display name shown to the user (e.g. in the draft editor's priority picker).</summary>
    public required string Name { get; init; }
    /// <summary>
    /// Gets the priority number stored via <see cref="IMessageFormat.SetPriority"/> and used verbatim as
    /// the OFT send priority (larger values are sent first — see <c>Docs/Peer.md</c>).
    /// </summary>
    public required int Value { get; init; }
}

/// <summary>Provides the set of message priority levels a user may choose from when composing a draft.</summary>
public interface IMessagePriorityProvider
{
    /// <summary>Returns every selectable priority level, in display order.</summary>
    IReadOnlyList<MessagePriorityOption> GetPriorities();
}

/// <summary>
/// Default <see cref="IMessagePriorityProvider"/> offering a single "Normal" priority level. Hosts that need
/// multiple selectable levels should override this registration with their own <see cref="IMessagePriorityProvider"/>.
/// </summary>
internal sealed class MessagePriorityProvider : IMessagePriorityProvider
{
    private static readonly IReadOnlyList<MessagePriorityOption> Priorities =
    [
        new MessagePriorityOption { Name = "Normal", Value = 0 }
    ];

    /// <inheritdoc />
    public IReadOnlyList<MessagePriorityOption> GetPriorities() => Priorities;
}
