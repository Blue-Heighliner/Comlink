namespace BlueHeighliner.Comlink;

/// <summary>Decides what the user may delete. State one with <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Deletes{THandler}"/>; without one everything may be deleted.</summary>
public interface IDeleteHandler
{
    /// <summary>Returns whether the entries and subfolders of the root folder described by <paramref name="context"/> can be deleted. It is a rule about root folders, never about an individual entry. When it returns <see langword="false"/> the delete is silently skipped and the delete controls are not offered.</summary>
    /// <param name="context">The root folder.</param>
    bool CanDelete(DeleteContext context);
}
