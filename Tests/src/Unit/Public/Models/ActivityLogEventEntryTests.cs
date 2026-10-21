namespace BlueHeighliner.Comlink.Tests.Unit.Public.Models;

/// <summary>Unit tests for the identifier of an exported <see cref="ActivityLogEventEntry"/>.</summary>
public sealed class ActivityLogEventEntryTests
{
    private static ActivityLogEventEntry Read(string eventId)
        => JsonSerializer.Deserialize<ActivityLogEventEntry>($$"""{"At":"2026-01-01T00:00:00Z","Message":"m","EventId":{{eventId}}}""")!;

    /// <summary>An identifier is written and read as text.</summary>
    [Fact]
    public void EventId_RoundTripsAsText()
    {
        ActivityLogEventEntry entry = new() { At = DateTime.UtcNow, Message = "m", EventId = "CL56" };

        string json = JsonSerializer.Serialize(entry);

        Assert.Contains("\"EventId\":\"CL56\"", json);
        Assert.Equal("CL56", JsonSerializer.Deserialize<ActivityLogEventEntry>(json)!.EventId);
    }

    /// <summary>A package written when identifiers were numbers is read with the engine's prefix in front, and zero is no identifier.</summary>
    [Theory]
    [InlineData("66", "CL66")]
    [InlineData("0", "")]
    [InlineData("null", "")]
    [InlineData("\"MINE7\"", "MINE7")]
    public void EventId_AcceptsTheNumbersOfOlderPackages(string json, string expected) => Assert.Equal(expected, Read(json).EventId);
}
