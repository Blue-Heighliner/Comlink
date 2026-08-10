namespace BlueHeighliner.Comlink.Engine.Data;

/// <summary>Identifies the kind of entry stored in a folder.</summary>
public enum EntryType
{
    /// <summary>An inbound or outbound message.</summary>
    Message,
    /// <summary>A message draft.</summary>
    Draft,
    /// <summary>A user note.</summary>
    Note,
    /// <summary>A daily activity log.</summary>
    Activity
}
