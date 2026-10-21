namespace BlueHeighliner.Comlink;

/// <summary>Formats a log line so that every field has the same width on every line, which keeps lines aligned.</summary>
internal interface ILogLineFormatter
{
    /// <summary>Formats one log line, without any exception text.</summary>
    /// <param name="at">When the event happened.</param>
    /// <param name="category">The category of the event.</param>
    /// <param name="user">The current user, or <see langword="null"/> before there is one.</param>
    /// <param name="eventId">The event being logged; its name is the identifier written, and an event with no name has none.</param>
    /// <param name="message">The formatted message.</param>
    string Format(DateTime at, string category, string? user, EventId eventId, string message);

    /// <summary>Formats the exception an entry reports, to follow its line: the exception's type and message and those of the exceptions inside it, or its whole text, stack trace included, when the host turned stack traces on.</summary>
    /// <param name="exception">The exception.</param>
    string FormatException(Exception exception);
}

/// <inheritdoc cref="ILogLineFormatter" />
/// <param name="engineController">Supplies the fixed widths the host gave the fields, if it did.</param>
internal sealed class LogLineFormatter(IEngineController engineController) : ILogLineFormatter
{
    /// <inheritdoc />
    public string Format(DateTime at, string category, string? user, EventId eventId, string message)
    {
        LogFieldWidths widths = engineController.LogWidths;
        string time = at.ToString("dd-MMM-yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture).ToUpperInvariant();
        string id = eventId.Name ?? string.Empty;
        return $"[{time}] [{Text(category.ToUpperInvariant(), widths.Category)}] [{Text(user ?? string.Empty, widths.User)}] [{Pad(id, widths.Id)}] {message}";
    }

    /// <inheritdoc />
    public string FormatException(Exception exception)
    {
        if (engineController.IsStackTraceEnabled)
        {
            return exception.ToString();
        }

        StringBuilder text = new();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            text.Append(text.Length == 0 ? string.Empty : " ---> ").Append(current.GetType().FullName).Append(": ").Append(current.Message);
        }

        return text.ToString();
    }

    // Fixed, text is cut to the width and padded with hyphens up to it; unfixed, it is left as it is.
    private static string Text(string text, int? width) => width is { } fixedWidth ? Pad(text.Length > fixedWidth ? text[..fixedWidth] : text, fixedWidth) : text;

    // Padding never cuts: an ID is cut by nobody, and the other fields cut before they pad.
    private static string Pad(string text, int? width) => width is { } fixedWidth ? text.PadRight(fixedWidth, '-') : text;
}
