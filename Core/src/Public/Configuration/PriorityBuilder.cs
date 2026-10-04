namespace BlueHeighliner.Comlink;

/// <summary>
/// Configures the priority levels the members of <typeparamref name="TPriority"/> stand for. Every member is a level, lowest first, so a member's position is its send priority: later members are sent before earlier ones.
/// Name a member with <see cref="Priority"/> to configure it, then continue with the next, for example <c>priorities.Priority(MessagePriority.Receipt).Mode(PriorityMode.System).Priority(MessagePriority.High).Label("URGENT")</c>.
/// </summary>
/// <typeparam name="TPriority">The enum whose members are the levels.</typeparam>
public interface IPriorityBuilder<TPriority> where TPriority : struct, Enum
{
    /// <summary>Selects <paramref name="priority"/> to configure its aspects.</summary>
    /// <param name="priority">The level, a member of <typeparamref name="TPriority"/>.</param>
    IPriorityLevelBuilder<TPriority> Priority(TPriority priority);

    /// <summary>Blocks a tag and priority combination when composing a draft. Either may be <see langword="null"/> to match any value, so a rule can block a tag whatever the priority, a priority whatever the tag, or one pair.</summary>
    /// <param name="tag">The tag to block (case-insensitive), or <see langword="null"/> for any tag.</param>
    /// <param name="priority">The level to block, or <see langword="null"/> for any level.</param>
    IPriorityBuilder<TPriority> Block(string? tag, TPriority? priority);
}
