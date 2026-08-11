namespace BlueHeighliner.Comlink.Engine.Logging;

/// <summary>Logger provider that writes log lines to a daily rolling file under the application data directory.</summary>
[ExcludeFromCodeCoverage]
public sealed class DailyFileLoggerProvider : ILoggerProvider
{
    private readonly IAppDataPathProvider _appDataPathProvider;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ConcurrentDictionary<string, DailyFileLogger> _loggers = new();
    private readonly object _writeLock = new();
    private string? _logDirectory;
    private Mutex? _crossProcessMutex;
    private DateOnly _currentDate;
    private StreamWriter? _writer;

    /// <summary>Initializes a new <see cref="DailyFileLoggerProvider"/> using the provided path and user providers.</summary>
    public DailyFileLoggerProvider(IAppDataPathProvider appDataPathProvider, ICurrentUserProvider currentUser)
    {
        _appDataPathProvider = appDataPathProvider;
        _currentUser = currentUser;
    }

    private string LogDirectory
    {
        get
        {
            if (_logDirectory is not null) return _logDirectory;
            string dir = Path.Combine(_appDataPathProvider.AppDataPath, "Logs");
            Directory.CreateDirectory(dir);
            _logDirectory = dir;
            // Named mutex keyed on log directory for cross-process write serialization.
            // "Local\" scope covers all sessions for the same user on this machine.
            string id = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(dir.ToLowerInvariant())));
            try { _crossProcessMutex = new Mutex(false, $"Local\\PCLog_{id}"); }
            catch { /* fall back to within-process lock only */ }
            return _logDirectory;
        }
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new DailyFileLogger(name, this, _currentUser));

    /// <summary>Writes a formatted log line to the current daily file under a cross-process mutex.</summary>
    internal void Write(string line)
    {
        bool mutexAcquired = false;
        try
        {
            try { mutexAcquired = _crossProcessMutex?.WaitOne(2000) ?? false; }
            catch (AbandonedMutexException) { mutexAcquired = true; } // other process crashed holding it

            lock (_writeLock)
            {
                DateOnly today = DateOnly.FromDateTime(DateTime.Now);
                if (_writer is null || today != _currentDate)
                {
                    _writer?.Dispose();
                    _currentDate = today;
                    string path = Path.Combine(LogDirectory, $"{today:yyyy-MM-dd}.log");
                    FileStream stream = new(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    _writer = new StreamWriter(stream) { AutoFlush = false };
                }
                _writer.WriteLine(line);
                _writer.Flush();
                Console.WriteLine(line);
            }
        }
        finally
        {
            if (mutexAcquired) _crossProcessMutex!.ReleaseMutex();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (DailyFileLogger logger in _loggers.Values)
            logger.Dispose();
        _loggers.Clear();
        _writer?.Dispose();
        _crossProcessMutex?.Dispose();
    }
}

/// <summary>Per-category logger that formats and forwards log entries to <see cref="DailyFileLoggerProvider"/>.</summary>
[ExcludeFromCodeCoverage]
public sealed class DailyFileLogger : ILogger, IDisposable
{
    private readonly string _categoryName;
    private readonly DailyFileLoggerProvider _provider;
    private readonly ICurrentUserProvider _currentUser;

    /// <summary>Initializes a new <see cref="DailyFileLogger"/> for the specified category.</summary>
    public DailyFileLogger(string categoryName, DailyFileLoggerProvider provider, ICurrentUserProvider currentUser)
    {
        _categoryName = categoryName;
        _provider = provider;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        string message = formatter(state, exception);
        string? user = _currentUser.UserName;
        DateTime dt = DateTime.Now;
        string dateStr = $"{dt.Day:D2}-{dt.ToString("MMM").ToUpperInvariant()}-{dt.Year} {dt:HH:mm}.{dt:fff}";
        string level = logLevel == LogLevel.Information ? "INFO" : logLevel.ToString().ToUpperInvariant();
        string category = _categoryName.ToUpperInvariant();
        string line = $"[{dateStr}] [{level}] [{category}] [{user ?? "----"}] {message}";
        if (exception is not null)
            line += Environment.NewLine + exception;

        _provider.Write(line);
    }

    /// <inheritdoc />
    public void Dispose() { }
}
