namespace BlueHeighliner.Comlink.Sample;

/// <summary>Lets users delete only what is in Drafts and Notes: Inbox, Outbox and Activity are protected.</summary>
public sealed class DeleteHandler : IDeleteHandler
{
    /// <inheritdoc />
    public bool CanDelete(DeleteContext context) => context.Folder is FolderType.Drafts or FolderType.Notes;
}
