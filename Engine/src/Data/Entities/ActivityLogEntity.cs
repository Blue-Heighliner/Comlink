namespace BlueHeighliner.Comlink.Engine.Data.Entities;

/// <summary>LiteDB document representing a single day's activity log.</summary>
public sealed class ActivityLogEntity
{
    /// <summary>Unique document identifier.</summary>
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    /// <summary>The UTC calendar date this log covers.</summary>
    public DateTime Date { get; set; }
    /// <summary>Legacy plain-text event strings (superseded by <see cref="EventEntries"/>).</summary>
    public List<string> Events { get; set; } = [];
    /// <summary>Structured log entries recorded for this day.</summary>
    public List<ActivityLogEntry> EventEntries { get; set; } = [];
    /// <summary>UTC timestamp when this document was first created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Identifier of the folder this log belongs to.</summary>
    public string FolderId { get; set; } = "root-activity";
}
