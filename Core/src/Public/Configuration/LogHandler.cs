namespace BlueHeighliner.Comlink;

/// <summary>
/// Controls the layout of the log lines the engine writes. State one with <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Logs{THandler}"/>. Each field of a line (category, user and event ID) can be given a fixed width so
/// that lines align field by field. By default no field is fixed: each is written at its natural length. Text in a fixed field is padded on the right with hyphens up to the width, and one that is longer is cut to it, except
/// the event ID, which is never cut since cutting it would name another event.
/// </summary>
public interface ILogHandler
{
    /// <summary>Gets the width of the category field, in characters, or <see langword="null"/> (the default) for none. The engine's own categories are <c>ACTIVITY</c>, <c>FRAMES</c>, <c>PACKETS</c>, <c>APP</c>, <c>SERIAL</c>, <c>IP</c>, <c>ERROR</c>, <c>CRASH</c> and <c>AVALONIA</c>, and <c>DEBUG</c> is for the host's own use.</summary>
    int? CategoryWidth => null;

    /// <summary>Gets the width of the user field, in characters, or <see langword="null"/> (the default) for none. A line written before the node's user is known has a user field of hyphens.</summary>
    int? UserWidth => null;

    /// <summary>Gets the width of the event ID field, in characters, or <see langword="null"/> (the default) for none. An ID is padded on the right with hyphens, never cut, and a line written without an event has an ID field of hyphens.</summary>
    int? IdWidth => null;

    /// <summary>
    /// Gets the categories that are written unless <c>Logging.json</c> or the <c>--log</c> argument turns more on, or <see langword="null"/> (the default) for the engine's own, <see cref="LogCategories.OnByDefault"/>: <c>ACTIVITY</c>, <c>APP</c>, <c>ERROR</c> and <c>CRASH</c>.
    /// It is the whole list, not an addition to the default one: a category left out is off, one of the engine's own as much as any other, so a host can silence <c>APP</c> or turn on <c>FRAMES</c>, or one of its own categories (see <see cref="ILogger"/>), from the start. <c>DEBUG</c> is not affected either way: it is written while debug mode is on and not otherwise.
    /// Names are compared in any case.
    /// </summary>
    IReadOnlyList<string>? CategoriesOnByDefault => null;
}
