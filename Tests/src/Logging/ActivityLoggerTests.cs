namespace BlueHeighliner.Comlink.Tests.Logging;

/// <summary>Unit tests for <see cref="ActivityLogger"/> and <see cref="ActivityLoggerProvider"/>.</summary>
public sealed class ActivityLoggerTests
{
    // ── ActivityLogger.IsEnabled ──────────────────────────────────────────────

    /// <summary>ACTIVITY category at Info level is enabled.</summary>
    [Fact]
    public void IsEnabled_ActivityCategory_InfoLevel_ReturnsTrue()
    {
        Mock<IActivityLogRepository> repo = new();
        ActivityLogger logger = new("ACTIVITY", repo.Object);
        Assert.True(logger.IsEnabled(LogLevel.Information));
    }

    /// <summary>ACTIVITY category at Debug level is disabled.</summary>
    [Fact]
    public void IsEnabled_ActivityCategory_DebugLevel_ReturnsFalse()
    {
        Mock<IActivityLogRepository> repo = new();
        ActivityLogger logger = new("ACTIVITY", repo.Object);
        Assert.False(logger.IsEnabled(LogLevel.Debug));
    }

    /// <summary>Non-ACTIVITY category is always disabled.</summary>
    [Theory]
    [InlineData("APP")]
    [InlineData("OTHER")]
    [InlineData("")]
    public void IsEnabled_NonActivityCategory_ReturnsFalse(string category)
    {
        Mock<IActivityLogRepository> repo = new();
        ActivityLogger logger = new(category, repo.Object);
        Assert.False(logger.IsEnabled(LogLevel.Information));
    }

    /// <summary>Category comparison is case-insensitive.</summary>
    [Fact]
    public void IsEnabled_ActivityCategoryLowercase_ReturnsTrue()
    {
        Mock<IActivityLogRepository> repo = new();
        ActivityLogger logger = new("activity", repo.Object);
        Assert.True(logger.IsEnabled(LogLevel.Information));
    }

    // ── ActivityLogger.BeginScope ─────────────────────────────────────────────

    /// <summary>BeginScope always returns null.</summary>
    [Fact]
    public void BeginScope_ReturnsNull()
    {
        Mock<IActivityLogRepository> repo = new();
        ActivityLogger logger = new("ACTIVITY", repo.Object);
        Assert.Null(logger.BeginScope("state"));
    }

    // ── ActivityLogger.Log ────────────────────────────────────────────────────

    /// <summary>Log at Info level calls AppendEvent on the repository.</summary>
    [Fact]
    public async Task Log_InfoLevel_CallsAppendEvent()
    {
        Mock<IActivityLogRepository> repo = new();
        repo.Setup(r => r.AppendEvent(It.IsAny<string>())).Returns(Task.CompletedTask);
        ActivityLogger logger = new("ACTIVITY", repo.Object);

        logger.Log(LogLevel.Information, default, "test message", null, (s, _) => s);

        await Task.Delay(50);
        repo.Verify(r => r.AppendEvent("test message"), Times.Once);
    }

    /// <summary>Log below Info level does not call AppendEvent.</summary>
    [Fact]
    public async Task Log_DebugLevel_DoesNotCallAppendEvent()
    {
        Mock<IActivityLogRepository> repo = new();
        ActivityLogger logger = new("ACTIVITY", repo.Object);

        logger.Log(LogLevel.Debug, default, "debug msg", null, (s, _) => s);

        await Task.Delay(50);
        repo.Verify(r => r.AppendEvent(It.IsAny<string>()), Times.Never);
    }

    /// <summary>Log on non-ACTIVITY category does not call AppendEvent.</summary>
    [Fact]
    public async Task Log_NonActivityCategory_DoesNotCallAppendEvent()
    {
        Mock<IActivityLogRepository> repo = new();
        ActivityLogger logger = new("APP", repo.Object);

        logger.Log(LogLevel.Information, default, "msg", null, (s, _) => s);

        await Task.Delay(50);
        repo.Verify(r => r.AppendEvent(It.IsAny<string>()), Times.Never);
    }

    // ── ActivityLoggerProvider ────────────────────────────────────────────────

    /// <summary>CreateLogger returns an ActivityLogger instance.</summary>
    [Fact]
    public void ActivityLoggerProvider_CreateLogger_ReturnsActivityLogger()
    {
        Mock<IActivityLogRepository> repo = new();
        ActivityLoggerProvider provider = new(repo.Object);
        ILogger logger = provider.CreateLogger("ACTIVITY");
        Assert.IsType<ActivityLogger>(logger);
    }

    /// <summary>Dispose does not throw.</summary>
    [Fact]
    public void ActivityLoggerProvider_Dispose_DoesNotThrow()
    {
        Mock<IActivityLogRepository> repo = new();
        ActivityLoggerProvider provider = new(repo.Object);
        provider.Dispose();
    }
}
