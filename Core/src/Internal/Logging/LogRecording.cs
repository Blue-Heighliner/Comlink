namespace BlueHeighliner.Comlink;

/// <summary>Writes an entry of one of the engine's events. The category it is logged under belongs to the event (see <see cref="LogEvents.CategoryOf"/>), not to the logger or to a level.</summary>
internal static class LogRecording
{
    // Classic extension methods rather than an extension block: the compiler reports a nullability error for the params array of one.
#pragma warning disable CA2254
    /// <summary>Records <paramref name="eventId"/> with its message.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="eventId">The event, one of <see cref="LogEvents"/>.</param>
    /// <param name="message">The message template.</param>
    /// <param name="args">The values of the template's placeholders.</param>
    public static void Record(this ILogger logger, EventId eventId, string message, params object?[] args) => logger.LogInformation(eventId, message, args);

    /// <summary>Records <paramref name="eventId"/> with its message and the exception that caused it.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="eventId">The event, one of <see cref="LogEvents"/>.</param>
    /// <param name="exception">The exception, written after the entry.</param>
    /// <param name="message">The message template.</param>
    /// <param name="args">The values of the template's placeholders.</param>
    public static void Record(this ILogger logger, EventId eventId, Exception? exception, string message, params object?[] args) => logger.LogInformation(eventId, exception, message, args);
#pragma warning restore CA2254
}
