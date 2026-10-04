namespace BlueHeighliner.Comlink;

/// <summary>What the engine asks an <see cref="IDeleteHandler"/> about: what the user is trying to delete from a root folder.</summary>
public sealed record DeleteContext
{
    /// <summary>Gets the root folder type of the folder whose entries, or subfolders, are being deleted.</summary>
    public required FolderType Folder { get; init; }
}
