namespace BlueHeighliner.Comlink.Engine.Data.Entities;

/// <summary>LiteDB document representing a folder in the folder tree.</summary>
public sealed class FolderEntity
{
    /// <summary>Unique folder identifier (well-known prefix "root-" for system folders).</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>Display name of the folder.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Root content type this folder belongs to.</summary>
    public FolderType RootType { get; set; }
    /// <summary>Identifier of the parent folder, or <c>null</c> for top-level folders.</summary>
    public string? ParentId { get; set; }
}
