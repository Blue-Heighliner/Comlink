namespace BlueHeighliner.Comlink;

/// <summary>
/// A custom auto forward controller: an option in the client's auto forward screen, shown to every user in <see cref="Users"/>, each of whom can open it there and maintain their own
/// locally-saved target list (added to and removed from freely, persisted between restarts). Whenever this instance receives a message (a frame the message handler recognizes;
/// other frames are never forwarded) that <see cref="Accepts"/> accepts, it is automatically forwarded, unchanged in body, to every user currently on that target list, with no action needed beyond
/// having set the target list up once. <see cref="Accepts"/> is never consulted for a user with no access, or with an empty target list, so an inaccessible or unconfigured controller costs nothing per
/// received message beyond that one check. Added with <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.AutoForward{TController}"/>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface IAutoForwardController<TFrame> where TFrame : class
{
    /// <summary>Gets the display name shown for this controller in the auto forward screen, and its key in local target-list storage. A later controller with the same name (case-insensitive) replaces an earlier one in place.</summary>
    string Name { get; }

    /// <summary>Gets the user names allowed to open this controller and maintain its target list.</summary>
    IReadOnlyList<string> Users { get; }

    /// <summary>Answers whether a received message should be auto-forwarded through this controller.</summary>
    /// <param name="frame">The received message.</param>
    bool Accepts(TFrame frame);
}
