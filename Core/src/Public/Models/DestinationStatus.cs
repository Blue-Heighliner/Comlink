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
    /// its initial status; on an Outbox record's per-destination status, it is set when the host's processor reports it
    /// (see <see cref="INetworkContext{TFrame, TPriority, TLevel, TAspect}.SetSentStatus(string, string, DestinationStatus)"/>), for example on receiving that destination's receive receipt.
    /// </summary>
    Received,
    /// <summary>
    /// The user has opened the message. On an Inbox record this is set locally when the user opens it.
    /// On an Outbox record's per-destination status, this is set only when the host's processor reports it
    /// (see <see cref="INetworkContext{TFrame, TPriority, TLevel, TAspect}.SetSentStatus(string, string, DestinationStatus)"/>), for example on receiving that destination's read receipt.
    /// </summary>
    Read
}
