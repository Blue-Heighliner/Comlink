namespace BlueHeighliner.Comlink.Tests.Unit.Internal.ViewModels;

/// <summary>Unit tests for <see cref="InstallViewModel"/>.</summary>
public sealed class InstallViewModelTests
{
    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public List<(string Category, LogLevel Level, string Message)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Entries);

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(string category, List<(string Category, LogLevel Level, string Message)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => entries.Add((LogEvents.CategoryOf(eventId) ?? category, logLevel, formatter(state, exception)));
        }
    }

    private static UserInfo MakeUserInfo(string name) => new()
    {
        Name = name
    };

    /// <summary>Empty UserName sets ErrorMessage without calling the service.</summary>
    [Fact]
    public async Task Install_EmptyUserName_SetsErrorMessage()
    {
        Mock<IEngineConnection> connMock = new();
        InstallViewModel vm = new(connMock.Object, LoggerFactory.Create(_ => { }), new TestEngineController());
        vm.UserName = "";

        await vm.InstallCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        connMock.Verify(c => c.InstallUser(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Valid name fires InstallSucceeded with the returned UserInfo.</summary>
    [Fact]
    public async Task Install_ValidName_FiresInstallSucceededWithUserInfo()
    {
        UserInfo expectedInfo = MakeUserInfo("ALPHA");
        Mock<IEngineConnection> connMock = new();
        connMock.Setup(c => c.InstallUser("USER1", It.IsAny<CancellationToken>())).ReturnsAsync(expectedInfo);
        InstallViewModel vm = new(connMock.Object, LoggerFactory.Create(_ => { }), new TestEngineController());
        vm.UserName = "USER1";

        UserInfo? received = null;
        vm.InstallSucceeded += info => { received = info; return Task.CompletedTask; };

        await vm.InstallCommand.ExecuteAsync(null);

        Assert.Equal("ALPHA", received?.Name);
        Assert.Null(vm.ErrorMessage);
    }

    /// <summary>Unknown name (null result) sets ErrorMessage and does not fire event.</summary>
    [Fact]
    public async Task Install_UnknownName_SetsErrorMessage()
    {
        Mock<IEngineConnection> connMock = new();
        connMock.Setup(c => c.InstallUser(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((UserInfo?)null);
        InstallViewModel vm = new(connMock.Object, LoggerFactory.Create(_ => { }), new TestEngineController());
        vm.UserName = "NOBODY";

        bool eventFired = false;
        vm.InstallSucceeded += _ => { eventFired = true; return Task.CompletedTask; };

        await vm.InstallCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.False(eventFired);
    }

    /// <summary>IsLoading is true during install and false after completion.</summary>
    [Fact]
    public async Task Install_IsLoadingLifecycle()
    {
        TaskCompletionSource<UserInfo?> gate = new();
        Mock<IEngineConnection> connMock = new();
        connMock.Setup(c => c.InstallUser(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(gate.Task);
        InstallViewModel vm = new(connMock.Object, LoggerFactory.Create(_ => { }), new TestEngineController());
        vm.UserName = "USER1";

        Task installTask = vm.InstallCommand.ExecuteAsync(null);
        Assert.True(vm.IsLoading);

        gate.SetResult(null);
        await installTask;
        Assert.False(vm.IsLoading);
    }

    /// <summary>UserName is automatically uppercased when set.</summary>
    [Fact]
    public void UserName_AutoUppercased()
    {
        Mock<IEngineConnection> connMock = new();
        InstallViewModel vm = new(connMock.Object, LoggerFactory.Create(_ => { }), new TestEngineController());

        vm.UserName = "user1";

        Assert.Equal("USER1", vm.UserName);
    }

    /// <summary>A failed install is written to the activity log, whether the name is unknown or the user's certificate is not in order.</summary>
    [Fact]
    public async Task Install_Failures_AreActivityLogged()
    {
        RecordingLoggerProvider provider = new();
        Mock<IEngineConnection> connMock = new();
        connMock.Setup(c => c.InstallUser("NOBODY", It.IsAny<CancellationToken>())).ReturnsAsync((UserInfo?)null);
        connMock.Setup(c => c.InstallUser("ALICE", It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("no certificate"));
        InstallViewModel vm = new(connMock.Object, LoggerFactory.Create(builder => builder.AddProvider(provider)), new TestEngineController());

        vm.UserName = "NOBODY";
        await vm.InstallCommand.ExecuteAsync(null);
        vm.UserName = "ALICE";
        await vm.InstallCommand.ExecuteAsync(null);

        Assert.All(provider.Entries, entry => Assert.Equal("ACTIVITY", entry.Category));
        Assert.Contains(provider.Entries, entry => entry.Message.Contains("NOBODY") && entry.Message.Contains("no such user"));
        Assert.Contains(provider.Entries, entry => entry.Message.Contains("ALICE") && entry.Message.Contains("no certificate"));
    }
}
