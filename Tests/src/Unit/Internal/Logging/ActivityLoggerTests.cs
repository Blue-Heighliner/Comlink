namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Logging;

/// <summary>Unit tests for <see cref="ActivityLogger"/> and <see cref="ActivityLoggerProvider"/>.</summary>
public sealed class ActivityLoggerTests
{
    private static ActivityLogger Build(Mock<IActivityLogRepository> repo, string category = "APP")
    {
        repo.Setup(r => r.AppendEvent(It.IsAny<string>(), It.IsAny<int>())).Returns(Task.CompletedTask);
        return new ActivityLogger(category, repo.Object);
    }

    /// <summary>BeginScope always returns null.</summary>
    [Fact]
    public void BeginScope_ReturnsNull()
    {
        Mock<IActivityLogRepository> repo = new();
        Assert.Null(Build(repo).BeginScope("state"));
    }

    /// <summary>An event of the activity category is appended to the activity log with its identifier, whatever logger it was written through.</summary>
    [Fact]
    public async Task Log_ActivityEvent_IsAppendedWithItsId()
    {
        Mock<IActivityLogRepository> repo = new();
        ActivityLogger logger = Build(repo);

        logger.Log(LogLevel.Information, LogEvents.AppStarted, "started", null, (s, _) => s);

        await Task.Delay(50);
        repo.Verify(r => r.AppendEvent("started", LogEvents.AppStarted.Id), Times.Once);
    }

    /// <summary>An event of any other category is not appended.</summary>
    [Theory]
    [MemberData(nameof(OtherCategoryEvents))]
    public async Task Log_EventOfAnotherCategory_IsNotAppended(EventId id)
    {
        Mock<IActivityLogRepository> repo = new();
        ActivityLogger logger = Build(repo, "ACTIVITY");

        logger.Log(LogLevel.Information, id, "msg", null, (s, _) => s);

        await Task.Delay(50);
        repo.Verify(r => r.AppendEvent(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    public static IEnumerable<object[]> OtherCategoryEvents() => [[LogEvents.UnhandledException], [LogEvents.ConnectionEventHandlerFailed], [LogEvents.CannotDeliverNoConnection], [LogEvents.FrameSent], [LogEvents.PacketReceived]];

    /// <summary>A message with no event of the engine is classified by the category of its logger, case-insensitively.</summary>
    [Theory]
    [InlineData("ACTIVITY", 1)]
    [InlineData("activity", 1)]
    [InlineData("APP", 0)]
    [InlineData("OTHER", 0)]
    [InlineData("", 0)]
    public async Task Log_WithoutAnEngineEvent_FollowsTheLoggerCategory(string category, int appended)
    {
        Mock<IActivityLogRepository> repo = new();
        ActivityLogger logger = Build(repo, category);

        logger.Log(LogLevel.Information, default, "msg", null, (s, _) => s);

        await Task.Delay(50);
        repo.Verify(r => r.AppendEvent("msg", 0), appended == 1 ? Times.Once() : Times.Never());
    }

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
