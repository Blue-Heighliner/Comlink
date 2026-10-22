namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="IDeleteContext"/> an <see cref="IDeleteHandler"/> is asked about.</summary>
/// <param name="folder">The root folder the context describes.</param>
public sealed class FakeDeleteContext(FolderType folder = FolderType.Inbox) : IDeleteContext
{
    /// <inheritdoc />
    public FolderType Folder { get; set; } = folder;
}
