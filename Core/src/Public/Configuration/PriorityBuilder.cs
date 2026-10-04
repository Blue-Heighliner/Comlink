namespace BlueHeighliner.Comlink;

/// <summary>
/// Configures the priority levels the members of <typeparamref name="TPriority"/> stand for, continuing the fluent chain of the engine builder. Every level must be stated, lowest first, with <see cref="Priority"/>: the order stated is the send priority, never the order of the enum, so later levels are sent before earlier ones, and a member not stated is not a level. Their integer values are how levels are stored in drafts and exports, so a member of the enum must never have its value changed or reused, even after it is no longer used, or data stored earlier would read as another level.
/// Name a member with <see cref="Priority"/> to configure it, then continue with the next, for example <c>.Priorities().Priority(MessagePriority.Receipt).Mode(PriorityMode.System).Priority(MessagePriority.High).Label("URGENT").KioskMode()</c>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/>.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the security levels.</typeparam>
public interface IPriorityBuilder<TFrame, TPacket, TPriority, TLevel> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum
{
    /// <summary>Selects <paramref name="priority"/> to configure its aspects.</summary>
    /// <param name="priority">The level, a member of <typeparamref name="TPriority"/>.</param>
    IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel> Priority(TPriority priority);

    /// <summary>Blocks a priority and tag combination when composing a draft. Either may be <see langword="null"/> to match any value, so a rule can block a priority whatever the tag, a tag whatever the priority, or one pair.</summary>
    /// <param name="priority">The level to block, or <see langword="null"/> for any level.</param>
    /// <param name="tag">The tag to block (case-insensitive), or <see langword="null"/> for any tag.</param>
    IPriorityBuilder<TFrame, TPacket, TPriority, TLevel> Block(TPriority? priority, string? tag);
}
