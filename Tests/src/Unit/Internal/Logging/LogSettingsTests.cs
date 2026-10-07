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
    }

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(directory, recursive: true);

    private void Write(string json) => File.WriteAllText(controller.Object.LoggingFilePath, json);

    /// <summary>By default only the traces are off.</summary>
    [Theory]
    [InlineData("ACTIVITY", true)]
    [InlineData("APP", true)]
    [InlineData("ERROR", true)]
    [InlineData("CRASH", true)]
    [InlineData("SomethingOfTheHost", true)]
    [InlineData("FRAMES", false)]
    [InlineData("PACKETS", false)]
    public void Defaults_OnlyTheTracesAreOff(string category, bool expected)
        => Assert.Equal(expected, new LogSettings(controller.Object, NetworkConfig.Load([], directory)).IsEnabled(category));

    /// <summary>Logging.json turns on the categories it names, in any case.</summary>
    [Fact]
    public void LoggingFile_EnablesTheCategoriesItNames()
    {
        Write("""["frames"]""");

        LogSettings settings = new(controller.Object, NetworkConfig.Load([], directory));

        Assert.True(settings.IsEnabled("FRAMES"));
        Assert.False(settings.IsEnabled("PACKETS"));
    }

    /// <summary>The --log argument turns on a comma separated list of categories, with or without the file.</summary>
    [Fact]
    public void LogArgument_EnablesTheCategoriesItNames()
    {
        Write("""["Frames"]""");

        LogSettings settings = new(controller.Object, NetworkConfig.Load(["--log", "Packets, other"], directory));

        Assert.True(settings.IsEnabled("FRAMES"));
        Assert.True(settings.IsEnabled("PACKETS"));
    }

    /// <summary>Reload applies a changed file, including its removal, and a file that is not JSON throws and keeps what was enabled.</summary>
    [Fact]
    public void Reload_FollowsTheFile()
    {
        LogSettings settings = new(controller.Object, NetworkConfig.Load([], directory));
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
