namespace BlueHeighliner.Comlink;

/// <summary>
/// Configures the message levels the members of <typeparamref name="TLevel"/> stand for, continuing the fluent chain of the engine builder. Every level must be stated, lowest first, with <see cref="Level"/>: the order stated is the ranking, never the order of the enum, and a member not stated is not a level. Their integer values are how levels are stored in drafts and exports, so a member of the enum must never have its value changed or reused, even after it is no longer used, or data stored earlier would read as another level.
/// Name a member with <see cref="Level"/> to configure it, then continue with the next, for example <c>.MessageLevels().Level(MessageLevel.Public).Color("#2E7D32").Level(MessageLevel.Restricted).Color("#C62828")</c>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/>.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects, or <see cref="NoMessageAspect"/> for none.</typeparam>
public interface IMessageLevelsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Selects <paramref name="level"/> to configure its aspects.</summary>
    /// <param name="level">The level, a member of <typeparamref name="TLevel"/>.</param>
    IMessageLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Level(TLevel level);
}
