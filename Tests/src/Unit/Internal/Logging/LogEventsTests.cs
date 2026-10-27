namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Logging;

/// <summary>Unit tests for <see cref="LogEvents"/>.</summary>
public sealed class LogEventsTests
{
    private static IReadOnlyList<EventId> All() => LogEvents.All;

    /// <summary>Every event has its own positive number, and the identifier it is logged with is that number after the CL prefix.</summary>
    [Fact]
    public void EveryEvent_HasAUniquePositiveNumberAndPrefixedId()
    {
        IReadOnlyList<EventId> events = All();

        Assert.NotEmpty(events);
        Assert.All(events, id => Assert.True(id.Id > 0));
        Assert.All(events, id => Assert.Equal($"CL{id.Id}", id.Name));
        Assert.Equal(events.Count, events.Select(id => id.Id).Distinct().Count());
        Assert.Equal(events.Count, events.Select(id => id.Name).Distinct().Count());
        Assert.Equal(events.Count, typeof(LogEvents).GetProperties(BindingFlags.Public | BindingFlags.Static).Count(property => property.PropertyType == typeof(EventId)));
    }

    /// <summary>An event that only has the number of one of the engine's, such as one a library logs, is not taken for it.</summary>
    [Fact]
    public void CategoryOf_IgnoresEventsThatAreNotTheEngines()
    {
        Assert.Equal(LogCategories.Crash, LogEvents.CategoryOf(LogEvents.UnhandledException));
        Assert.Null(LogEvents.CategoryOf(new EventId(LogEvents.UnhandledException.Id)));
        Assert.Null(LogEvents.CategoryOf(new EventId(LogEvents.UnhandledException.Id, "Starting")));
        Assert.Null(LogEvents.CategoryOf(new EventId(0, "CL1")));
    }

    /// <summary>Every event in the code appears in the log document under its identifier.</summary>
    [Fact]
    public void EveryEvent_IsInTheLogDocument()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "Docs", "Components", "Logging.md")))
        {
            directory = Path.GetDirectoryName(directory);
        }
        string document = File.ReadAllText(Path.Combine(directory ?? throw new FileNotFoundException("Logging.md was not found above the test output"), "Docs", "Components", "Logging.md"));

        foreach (EventId id in All())
        {
            Assert.Contains($"| {id.Name} |", document);
        }
    }

    /// <summary>Every event belongs to one of the engine's categories (DEBUG has none, since only a host writes under it), and appears in the log document under the heading of its category.</summary>
    [Fact]
    public void EveryEvent_HasACategory_AndIsDocumentedUnderIt()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "Docs", "Components", "Logging.md")))
        {
            directory = Path.GetDirectoryName(directory);
        }
        string[] lines = File.ReadAllLines(Path.Combine(directory ?? throw new FileNotFoundException("Logging.md was not found above the test output"), "Docs", "Components", "Logging.md"));
        string[] categories = ["ACTIVITY", "FRAMES", "PACKETS", "SERIAL", "IP", "AVALONIA", "APP", "ERROR", "CRASH"];

        foreach (EventId id in All())
        {
            string category = Assert.IsType<string>(LogEvents.CategoryOf(id));
            Assert.Contains(category, categories);
            int heading = Array.FindIndex(lines, line => line == $"### {category}");
            int row = Array.FindIndex(lines, line => line.StartsWith($"| {id.Name} |", StringComparison.Ordinal));
            int next = Array.FindIndex(lines, heading + 1, line => line.StartsWith("### ", StringComparison.Ordinal));
            Assert.True(heading >= 0 && row > heading && (next < 0 || row < next), $"{id.Name} is not documented under {category}");
        }
    }
}
