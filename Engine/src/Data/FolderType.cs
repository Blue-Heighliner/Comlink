namespace BlueHeighliner.Comlink.Engine.Data;

/// <summary>Identifies the content category that a root folder holds.</summary>
public enum FolderType
{
    /// <summary>Holds received messages.</summary>
    Inbox,
    /// <summary>Holds sent messages.</summary>
    Outbox,
    /// <summary>Holds draft messages.</summary>
    Drafts,
    /// <summary>Holds user notes.</summary>
    Notes,
    /// <summary>Holds daily activity logs.</summary>
    Activity
}
