namespace BlueHeighliner.Comlink;

/// <summary>Logger provider that routes the log entries of the activity category into the <see cref="ActivityLogRepository"/>.</summary>
internal sealed class ActivityLoggerProvider : ILoggerProvider
{
    /// <summary>Initializes a new <see cref="ActivityLoggerProvider"/> using the given repository.</summary>
    public ActivityLoggerProvider(IActivityLogRepository repository) => this.repository = repository;

    private readonly IActivityLogRepository repository;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new ActivityLogger(categoryName, repository);

    /// <inheritdoc />
    public void Dispose() { }
}

/// <summary>Logger that appends messages to the daily activity log for the events whose category is "ACTIVITY".</summary>
internal sealed class ActivityLogger : ILogger
{
    /// <summary>Initializes a new <see cref="ActivityLogger"/> for the specified category.</summary>
    public ActivityLogger(string categoryName, IActivityLogRepository repository)
    {
        this.categoryName = categoryName;
        this.repository = repository;
    }

    private readonly string categoryName;
    private readonly IActivityLogRepository repository;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        string category = LogEvents.CategoryOf(eventId) ?? categoryName;
        if (!string.Equals(category, LogCategories.Activity, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        _ = Write(formatter(state, exception), eventId.Id);
    }

    private async Task Write(string message, int eventId)
    {
        try { await repository.AppendEvent(message, eventId); }
        catch { }
    }
}
