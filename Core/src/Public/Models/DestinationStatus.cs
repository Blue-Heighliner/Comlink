namespace BlueHeighliner.Comlink;

/// <summary>
/// Represents the delivery/read state of a message, either as its own status on an Inbox record
/// (<see cref="Received"/>/<see cref="Read"/> only) or as the per-destination status on an Outbox
/// record's <see cref="DeliveryStatus"/> (Sending, Sent, Received, Read or Failed; see <c>Docs/Components/Peer.md</c>).
/// </summary>
public enum DestinationStatus
{
    /// <summary>The message is currently being transmitted.</summary>
    Sending,
    /// <summary>The message left this computer but the destination has not yet reported receiving it.</summary>
    Sent,
    /// <summary>Delivery failed with an error.</summary>
    Failed,
    /// <summary>
    /// The message has arrived at the destination but the user has not yet opened it. On an Inbox record this is
    /// its initial status; on an Outbox record's per-destination status, it is set when the sender receives that
    /// destination's receive receipt frame.
    /// </summary>
    Received,
    /// <summary>
    /// The user has opened the message. On an Inbox record this is set locally when the user opens it.
    /// On an Outbox record's per-destination status, this is set only after the sender receives that
    /// destination's read receipt frame (see <see cref="IFrameBuilder{TFrame}"/>).
    /// </summary>
    Read
}
