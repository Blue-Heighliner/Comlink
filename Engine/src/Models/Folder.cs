namespace BlueHeighliner.Comlink.Engine.Models;

/// <summary>A node in the folder hierarchy exposed to the application layer.</summary>
public sealed class Folder
{
    /// <summary>Unique folder identifier.</summary>
    public required string Id { get; init; }
    /// <summary>Display name of the folder.</summary>
    public required string Name { get; init; }
    /// <summary>Root content type this folder belongs to.</summary>
    public required FolderType RootType { get; init; }
    /// <summary>Identifier of the parent folder, or <c>null</c> for top-level folders.</summary>
    public string? ParentId { get; init; }
    /// <summary>Child folders nested under this folder.</summary>
    public IReadOnlyList<Folder> Children { get; init; } = [];
}
