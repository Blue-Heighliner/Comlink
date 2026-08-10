namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="ActivityLogViewModel"/>.</summary>
public sealed class ActivityLogViewModelTests
{
    private static ActivityLogEntity MakeEntity(DateTime date, List<string>? legacyEvents = null, List<ActivityLogEntry>? structured = null)
        => new()
        {
            Date = date,
            Events = legacyEvents ?? [],
            EventEntries = structured ?? []
        };

    // ── Date formatting ───────────────────────────────────────────────────────

    /// <summary>Date is formatted as uppercase DD-MMM-YYYY.</summary>
    [Fact]
    public void Date_IsFormattedCorrectly()
    {
        ActivityLogEntity entity = MakeEntity(new DateTime(2025, 7, 4));

        ActivityLogViewModel vm = new(entity);

        Assert.Equal("04-JUL-2025", vm.Date);
    }

    // ── Event ordering ────────────────────────────────────────────────────────

    /// <summary>Events are ordered newest-first when all are structured entries.</summary>
    [Fact]
    public void Events_OrderedNewestFirst_StructuredEntries()
    {
        DateTime date = new(2025, 7, 4);
        DateTime older = new(2025, 7, 4, 8, 0, 0, DateTimeKind.Utc);
        DateTime newer = new(2025, 7, 4, 10, 0, 0, DateTimeKind.Utc);
        ActivityLogEntity entity = MakeEntity(date, structured:
        [
            new ActivityLogEntry { At = older, Message = "First" },
            new ActivityLogEntry { At = newer, Message = "Second" }
        ]);

        ActivityLogViewModel vm = new(entity);

        Assert.Equal(2, vm.Events.Count);
        Assert.Equal("Second", vm.Events[0].Message);
        Assert.Equal("First", vm.Events[1].Message);
    }

    /// <summary>Legacy string events are merged with structured entries and ordered correctly.</summary>
    [Fact]
    public void Events_LegacyAndStructured_MergedAndOrdered()
    {
        DateTime date = new(2025, 7, 4);
        ActivityLogEntity entity = new()
        {
            Date = date,
            Events = ["Legacy message"],
            EventEntries =
            [
                new ActivityLogEntry { At = new DateTime(2025, 7, 4, 12, 0, 0, DateTimeKind.Utc), Message = "Structured" }
            ]
        };

        ActivityLogViewModel vm = new(entity);

        Assert.Equal(2, vm.Events.Count);
        Assert.Equal("Structured", vm.Events[0].Message);
        Assert.Equal("Legacy message", vm.Events[1].Message);
    }

    // ── TimeText ──────────────────────────────────────────────────────────────

    /// <summary>TimeText on an event row is formatted as uppercase DD-MMM-YYYY HH:mm.</summary>
    [Fact]
    public void EventRow_TimeText_IsFormattedCorrectly()
    {
        DateTime date = new(2025, 7, 4);
        ActivityLogEntity entity = MakeEntity(date, structured:
        [
            new ActivityLogEntry { At = new DateTime(2025, 7, 4, 9, 30, 0, DateTimeKind.Utc), Message = "Test" }
        ]);

        ActivityLogViewModel vm = new(entity);

        Assert.Equal("04-JUL-2025 09:30", vm.Events[0].TimeText);
    }
}
