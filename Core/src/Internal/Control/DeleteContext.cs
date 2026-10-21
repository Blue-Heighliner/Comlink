namespace BlueHeighliner.Comlink;

/// <summary>The engine's implementation of what a host's <see cref="IDeleteHandler"/> is asked about.</summary>
internal sealed record DeleteContext : IDeleteContext
{
    /// <inheritdoc />
    public required FolderType Folder { get; init; }
}
