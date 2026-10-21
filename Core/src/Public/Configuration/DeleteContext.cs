namespace BlueHeighliner.Comlink;

/// <summary>What the engine asks an <see cref="IDeleteHandler"/> about: what the user is trying to delete from a root folder.</summary>
public interface IDeleteContext
{
    /// <summary>Gets the root folder type of the folder whose entries, or subfolders, are being deleted.</summary>
    FolderType Folder { get; }
}
