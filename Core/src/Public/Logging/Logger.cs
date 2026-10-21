namespace BlueHeighliner.Comlink;

/// <summary>
/// Writes to the engine's log, for any component of the host (a handler, a hosted service, anything built through dependency injection) without having to know how the engine logs. Ask for it through dependency injection.
/// An entry has a category and an identifier of the event it reports, which the line carries and the activity log lists. The engine's own identifiers are <c>CL</c> and a number, such as <c>CL56</c>, so a host
/// gives its own a prefix of its own, such as <c>MYAPP7</c>. Whether an entry is written depends on its category: the engine's own (<see cref="LogCategories.Activity"/>, <see cref="LogCategories.App"/>, <see cref="LogCategories.Error"/>
/// and <see cref="LogCategories.Crash"/>) always are, and any other is written only once <c>Logging.json</c> or the <c>--log</c> argument names it. An <see cref="LogCategories.Activity"/> entry also appears in the activity log the user reads.
/// What is built before the engine runs, the configuration and the display handler, gets a log that writes nothing, since the engine's logging does not exist yet.
/// </summary>
public interface ILogger
{
    /// <summary>Writes an entry.</summary>
    /// <param name="category">The category, in any case; see <see cref="LogCategories"/> for the engine's own.</param>
    /// <param name="id">The identifier of the kind of event this is, such as <c>MYAPP7</c>; the engine's own start with <c>CL</c>.</param>
    /// <param name="message">What to write.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is empty or only white space.</exception>
    void Log(string category, string id, string message);

    /// <summary>Writes an entry that reports an exception. The line carries the exception's type and message, and, unless <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.StackTrace"/> was turned off, its whole stack trace too.</summary>
    /// <param name="category">The category, in any case; see <see cref="LogCategories"/> for the engine's own.</param>
    /// <param name="id">The identifier of the kind of event this is, such as <c>MYAPP7</c>; the engine's own start with <c>CL</c>.</param>
    /// <param name="exception">The exception to report.</param>
    /// <param name="message">What to write.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is empty or only white space.</exception>
    void Log(string category, string id, Exception exception, string message);
}
