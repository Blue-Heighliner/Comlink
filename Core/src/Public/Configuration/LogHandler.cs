namespace BlueHeighliner.Comlink;

/// <summary>
/// Controls the layout of the log lines the engine writes. State one with <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Logs{THandler}"/>. Each field of a line (category, user and event ID) can be given a fixed width so
/// that lines align field by field. By default no field is fixed: each is written at its natural length. Text in a fixed field is padded on the right with hyphens up to the width, and one that is longer is cut to it, except
/// the event ID, which is never cut since cutting it would name another event.
/// </summary>
public interface ILogHandler
{
    /// <summary>Gets the width of the category field, in characters, or <see langword="null"/> (the default) for none. The engine's own categories are <c>ACTIVITY</c>, <c>FRAMES</c>, <c>PACKETS</c>, <c>APP</c>, <c>ERROR</c> and <c>CRASH</c>.</summary>
    int? CategoryWidth => null;

    /// <summary>Gets the width of the user field, in characters, or <see langword="null"/> (the default) for none. A line written before a user is installed or named has a user field of hyphens.</summary>
    int? UserWidth => null;

    /// <summary>Gets the width of the event ID field, in characters, or <see langword="null"/> (the default) for none. An ID is padded on the right with hyphens, never cut, and a line written without an event has an ID field of hyphens.</summary>
    int? IdWidth => null;
}
