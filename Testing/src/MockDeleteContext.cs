namespace BlueHeighliner.Comlink.Testing;

/// <summary>A mock of the <see cref="IDeleteContext"/> an <see cref="IDeleteHandler"/> is asked about.</summary>
public sealed class MockDeleteContext : Mock<IDeleteContext>
{
    /// <summary>Initializes the mock.</summary>
    /// <param name="folder">The root folder the context describes.</param>
    public MockDeleteContext(FolderType folder = FolderType.Inbox)
    {
        Folder = folder;
        Setup(context => context.Folder).Returns(() => Folder);
    }

    /// <summary>Gets or sets what <see cref="IDeleteContext.Folder"/> returns.</summary>
    public FolderType Folder { get; set; }
}
