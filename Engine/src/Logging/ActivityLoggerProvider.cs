namespace BlueHeighliner.Comlink.Engine.Logging;

/// <summary>Logger provider that routes ACTIVITY-category log entries into the <see cref="ActivityLogRepository"/>.</summary>
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

/// <summary>Logger that appends messages to the daily activity log when the category is "ACTIVITY".</summary>
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
    public bool IsEnabled(LogLevel logLevel)
        => string.Equals(categoryName, "ACTIVITY", StringComparison.OrdinalIgnoreCase) &&
        logLevel >= LogLevel.Information;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) { return; }
        string message = formatter(state, exception);
        _ = Write(message);
    }

    private async Task Write(string message)
    {
        try { await repository.AppendEvent(message); }
        catch { }
    }
}
