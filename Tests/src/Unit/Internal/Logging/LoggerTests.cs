namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Logging;

/// <summary>Unit tests for <see cref="Logger"/>.</summary>
public sealed class LoggerTests
{
    private sealed class Recorder : ILoggerProvider
    {
        public List<(string Category, EventId Event, string Message, Exception? Exception)> Entries { get; } = [];

        public MicrosoftLogger CreateLogger(string categoryName) => new Recording(categoryName, Entries);

        public void Dispose()
        {
        }

        private sealed class Recording(string category, List<(string, EventId, string, Exception?)> entries) : MicrosoftLogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => entries.Add((category, eventId, formatter(state, exception), exception));
        }
    }

    private static (Logger Log, Recorder Recorder) Build()
    {
        Recorder recorder = new();
        return (new Logger(LoggerFactory.Create(builder => builder.AddProvider(recorder))), recorder);
    }

    /// <summary>An entry reaches the providers under its category, with its message and its identifier as the name of the event.</summary>
    [Fact]
    public void Write_ReachesTheProvidersUnderItsCategoryWithItsId()
    {
        (Logger log, Recorder recorder) = Build();

        log.Log(LogCategories.Activity, "MINE1", "hello");

        (string category, EventId id, string message, Exception? exception) = Assert.Single(recorder.Entries);
        Assert.Equal((LogCategories.Activity, "MINE1", "hello", null), (category, id.Name, message, exception));
    }

    /// <summary>An entry that reports an exception hands the exception on, so the engine's logging decides what of it is written.</summary>
    [Fact]
    public void Write_WithAnException_HandsTheExceptionOn()
    {
        (Logger log, Recorder recorder) = Build();
        InvalidOperationException exception = new("bad");

        log.Log("MINE", "MINE2", exception, "it broke");

        (string category, EventId id, string message, Exception? reported) = Assert.Single(recorder.Entries);
        Assert.Equal(("MINE", "MINE2", "it broke"), (category, id.Name, message));
        Assert.Same(exception, reported);
    }

    /// <summary>Entries of different categories go to loggers of their own, and a category is the same whatever its case.</summary>
    [Fact]
    public void Write_UsesOneLoggerPerCategory()
    {
        (Logger log, Recorder recorder) = Build();

        log.Log("Mine", "M1", "a");
        log.Log("MINE", "M1", "b");
        log.Log("Other", "M1", "c");

        Assert.Equal(["Mine", "Mine", "Other"], recorder.Entries.Select(entry => entry.Category));
    }

    /// <summary>An entry with no identifier is refused, since every entry names the kind of event it is.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Write_WithoutAnId_IsRefused(string id)
    {
        (Logger log, Recorder recorder) = Build();

        Assert.Throws<ArgumentException>(() => log.Log("MINE", id, "a"));
        Assert.Throws<ArgumentException>(() => log.Log("MINE", id, new InvalidOperationException(), "a"));
        Assert.Throws<ArgumentNullException>(() => log.Log("MINE", null!, "a"));
        Assert.Empty(recorder.Entries);
    }
}
