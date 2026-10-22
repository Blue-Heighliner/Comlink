namespace BlueHeighliner.Comlink.Tests.Unit.Public.Models;

/// <summary>Unit tests for the identifier of an exported <see cref="ActivityLogEventEntry"/>.</summary>
public sealed class ActivityLogEventEntryTests
{
    /// <summary>An identifier is written and read as text.</summary>
    [Fact]
    public void EventId_RoundTripsAsText()
    {
        ActivityLogEventEntry entry = new() { At = DateTime.UtcNow, Message = "m", EventId = "CL56" };

        string json = JsonSerializer.Serialize(entry);

        Assert.Contains("\"EventId\":\"CL56\"", json);
        Assert.Equal("CL56", JsonSerializer.Deserialize<ActivityLogEventEntry>(json)!.EventId);
    }
}
