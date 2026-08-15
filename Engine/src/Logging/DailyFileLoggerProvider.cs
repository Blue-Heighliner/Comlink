namespace BlueHeighliner.Comlink.Engine.Logging;

/// <summary>Logger provider that writes log lines to a daily rolling file under the application data directory.</summary>
[ExcludeFromCodeCoverage]
public sealed class DailyFileLoggerProvider : ILoggerProvider
{
    /// <summary>Initializes a new <see cref="DailyFileLoggerProvider"/> using the provided path and user providers.</summary>
    public DailyFileLoggerProvider(IEngineController engineController, ICurrentUserProvider currentUser)
    {
        this.engineController = engineController;
        this.currentUser = currentUser;
    }

    private readonly IEngineController engineController;
    private readonly ICurrentUserProvider currentUser;

    private readonly ConcurrentDictionary<string, DailyFileLogger> _loggers = new();

    private readonly object writeLock = new();
    private string? logDirectory;
    private Mutex? crossProcessMutex;
    private DateOnly currentDate;
    private StreamWriter? writer;

    private string LogDirectory
    {
        get
        {
            if (logDirectory is not null) { return logDirectory; }
            string dir = Path.Combine(engineController.AppDataPath, "Logs");
            Directory.CreateDirectory(dir);
            logDirectory = dir;
            // Named mutex keyed on log directory for cross-process write serialization.
            // "Local\" scope covers all sessions for the same user on this machine.
            string id = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(dir.ToLowerInvariant())));
            try { crossProcessMutex = new Mutex(false, $"Local\\PCLog_{id}"); }
            catch { /* fall back to within-process lock only */ }
            return logDirectory;
        }
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, name => new DailyFileLogger(name, this, currentUser));

    /// <summary>Writes a formatted log line to the current daily file under a cross-process mutex.</summary>
    internal void Write(string line)
    {
        bool mutexAcquired = false;
        try
        {
            try { mutexAcquired = crossProcessMutex?.WaitOne(2000) ?? false; }
            catch (AbandonedMutexException) { mutexAcquired = true; } // other process crashed holding it

            lock (writeLock)
            {
                DateOnly today = DateOnly.FromDateTime(DateTime.Now);
                if (writer is null || today != currentDate)
                {
                    writer?.Dispose();
                    currentDate = today;
                    string path = Path.Combine(LogDirectory, $"{today:yyyy-MM-dd}.log");
                    FileStream stream = new(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    writer = new StreamWriter(stream) { AutoFlush = false };
                }
                writer.WriteLine(line);
                writer.Flush();
                Console.WriteLine(line);
            }
        }
        finally
        {
            if (mutexAcquired) { crossProcessMutex!.ReleaseMutex(); }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (DailyFileLogger logger in _loggers.Values)
        {
            logger.Dispose();
        }
        _loggers.Clear();
        writer?.Dispose();
        crossProcessMutex?.Dispose();
    }

}

/// <summary>Per-category logger that formats and forwards log entries to <see cref="DailyFileLoggerProvider"/>.</summary>
[ExcludeFromCodeCoverage]
public sealed class DailyFileLogger : ILogger, IDisposable
{
    /// <summary>Initializes a new <see cref="DailyFileLogger"/> for the specified category.</summary>
    public DailyFileLogger(string categoryName, DailyFileLoggerProvider provider, ICurrentUserProvider currentUser)
    {
        this.categoryName = categoryName;
        this.provider = provider;
        this.currentUser = currentUser;
    }

    private readonly string categoryName;
    private readonly DailyFileLoggerProvider provider;
    private readonly ICurrentUserProvider currentUser;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) { return; }
        string message = formatter(state, exception);
        string? user = currentUser.UserName;
        DateTime dt = DateTime.Now;
        string dateStr = $"{dt.Day:D2}-{dt.ToString("MMM").ToUpperInvariant()}-{dt.Year} {dt:HH:mm}.{dt:fff}";
        string level = logLevel == LogLevel.Information ? "INFO" : logLevel.ToString().ToUpperInvariant();
        string category = categoryName.ToUpperInvariant();
        string line = $"[{dateStr}] [{level}] [{category}] [{user ?? "----"}] {message}";
        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        provider.Write(line);
    }

    /// <inheritdoc />
    public void Dispose() { }
}
