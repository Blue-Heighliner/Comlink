namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Logging;

/// <summary>Unit tests for <see cref="LogSettings"/>.</summary>
public sealed class LogSettingsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"comlink-logging-{Guid.NewGuid():N}");
    private readonly Mock<IEngineController> controller = new();

    /// <summary>Points the controller at a Logging.json in a temp folder.</summary>
    public LogSettingsTests()
    {
        Directory.CreateDirectory(directory);
        controller.SetupGet(c => c.LoggingFilePath).Returns(Path.Combine(directory, "Logging.json"));
        controller.SetupGet(c => c.LogCategoriesOnByDefault).Returns(LogCategories.OnByDefault);
    }

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(directory, recursive: true);

    private void Write(string json) => File.WriteAllText(controller.Object.LoggingFilePath, json);

    /// <summary>By default only the categories on the whitelist are written, so the traces, Avalonia's and a host's own are off.</summary>
    [Theory]
    [InlineData("ACTIVITY", true)]
    [InlineData("APP", true)]
    [InlineData("ERROR", true)]
    [InlineData("CRASH", true)]
    [InlineData("SomethingOfTheHost", false)]
    [InlineData("AVALONIA", false)]
    [InlineData("FRAMES", false)]
    [InlineData("PACKETS", false)]
    [InlineData("SERIAL", false)]
    [InlineData("IP", false)]
    [InlineData("DEBUG", false)]
    public void Defaults_OnlyTheWhitelistedCategoriesAreOn(string category, bool expected)
        => Assert.Equal(expected, new LogSettings(controller.Object, new FakeDebugMode(), NetworkConfig.Load([], directory)).IsEnabled(category));

    /// <summary>A list from the host replaces the whitelist: its categories are on in any case, the engine's own that it leaves out are off, and Logging.json still adds.</summary>
    [Fact]
    public void HostList_ReplacesTheWhitelist()
    {
        controller.SetupGet(c => c.LogCategoriesOnByDefault).Returns(["frames", "SomethingOfTheHost", "APP"]);
        Write("""["Packets"]""");

        LogSettings settings = new(controller.Object, new FakeDebugMode(), NetworkConfig.Load([], directory));

        Assert.True(settings.IsEnabled("FRAMES"));
        Assert.True(settings.IsEnabled("somethingofthehost"));
        Assert.True(settings.IsEnabled("APP"));
        Assert.True(settings.IsEnabled("PACKETS"));
        Assert.False(settings.IsEnabled("ERROR"));
        Assert.False(settings.IsEnabled("ACTIVITY"));
    }

    /// <summary>Logging.json turns on the categories it names, in any case.</summary>
    [Fact]
    public void LoggingFile_EnablesTheCategoriesItNames()
    {
        Write("""["frames"]""");

        LogSettings settings = new(controller.Object, new FakeDebugMode(), NetworkConfig.Load([], directory));

        Assert.True(settings.IsEnabled("FRAMES"));
        Assert.False(settings.IsEnabled("PACKETS"));
    }

    /// <summary>The DEBUG category is written exactly while debug mode is on, whatever the file, the command line or the log handler says.</summary>
    [Fact]
    public void Debug_FollowsDebugModeAlone()
    {
        Write("""["Debug"]""");
        NetworkConfig named = NetworkConfig.Load(["--log", "debug"], directory);
        controller.SetupGet(c => c.LogCategoriesOnByDefault).Returns(["DEBUG"]);
        FakeDebugMode debug = new(isDebug: false);
        LogSettings settings = new(controller.Object, debug, named);

        Assert.False(settings.IsEnabled("DEBUG"));

        debug.IsDebug = true;
        controller.SetupGet(c => c.LogCategoriesOnByDefault).Returns([]);

        Assert.True(settings.IsEnabled("debug"));
    }

    /// <summary>A category the engine has no switch for, such as a host's own, is written only once Logging.json names it.</summary>
    [Fact]
    public void LoggingFile_EnablesAHostsOwnCategory()
    {
        Write("""["SomethingOfTheHost"]""");

        Assert.True(new LogSettings(controller.Object, new FakeDebugMode(), NetworkConfig.Load([], directory)).IsEnabled("SOMETHINGOFTHEHOST"));
    }

    /// <summary>The --log argument turns on a comma separated list of categories, with or without the file.</summary>
    [Fact]
    public void LogArgument_EnablesTheCategoriesItNames()
    {
        Write("""["Frames"]""");

        LogSettings settings = new(controller.Object, new FakeDebugMode(), NetworkConfig.Load(["--log", "Packets, other"], directory));

        Assert.True(settings.IsEnabled("FRAMES"));
        Assert.True(settings.IsEnabled("PACKETS"));
    }

    /// <summary>Reload applies a changed file, including its removal, and a file that is not JSON throws and keeps what was enabled.</summary>
    [Fact]
    public void Reload_FollowsTheFile()
    {
        LogSettings settings = new(controller.Object, new FakeDebugMode(), NetworkConfig.Load([], directory));
        Assert.False(settings.IsEnabled("PACKETS"));

        Write("""["Packets"]""");
        settings.Reload();
        Assert.True(settings.IsEnabled("PACKETS"));

        Write("{ not json");
        Assert.Throws<InvalidDataException>(settings.Reload);
        Assert.True(settings.IsEnabled("PACKETS"));

        File.Delete(controller.Object.LoggingFilePath);
        settings.Reload();
        Assert.False(settings.IsEnabled("PACKETS"));
    }
}
