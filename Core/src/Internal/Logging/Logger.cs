namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="ILogger" />
/// <param name="loggerFactory">Creates the logger of each category, which every provider the engine has registered writes through.</param>
internal sealed class Logger(ILoggerFactory loggerFactory) : ILogger
{
    private readonly ConcurrentDictionary<string, MicrosoftLogger> loggers = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Log(string category, string id, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        LoggerOf(category).Log(LogLevel.Information, new EventId(0, id), message, null, (text, _) => text);
    }

    /// <inheritdoc />
    public void Log(string category, string id, Exception exception, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        LoggerOf(category).Log(LogLevel.Information, new EventId(0, id), message, exception, (text, _) => text);
    }

    private MicrosoftLogger LoggerOf(string category) => loggers.GetOrAdd(category, loggerFactory.CreateLogger);
}
