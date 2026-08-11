namespace BlueHeighliner.Comlink.Engine.Data;

/// <summary>
/// Represents the delivery/read state of a message, either as its own status on an Inbox record
/// (<see cref="Received"/>/<see cref="Read"/> only) or as the per-destination status on an Outbox
/// record's <see cref="Entities.DeliveryStatus"/> (all values except <see cref="Received"/>, which has
/// no outbound equivalent — see <c>Docs/Peer.md</c>).
/// </summary>
public enum DestinationStatus
{
    /// <summary>The message is currently being transmitted.</summary>
    Sending,
    /// <summary>The message was transmitted but delivery has not been confirmed.</summary>
    Sent,
    /// <summary>Delivery failed with an error.</summary>
    Failed,
    /// <summary>The destination user acknowledged receipt.</summary>
    Confirmed,
    /// <summary>
    /// Inbox-only: the message has arrived and is stored, but the user has not yet opened it. Never
    /// appears on an Outbox record's per-destination status.
    /// </summary>
    Received,
    /// <summary>
    /// The user has opened the message. On an Inbox record this is set locally when the user opens it.
    /// On an Outbox record's per-destination status, this is set only after the sender receives that
    /// destination's user-read confirmation message (see <see cref="Control.IMessageFormat.GetConfirmationMessageId"/>).
    /// </summary>
    Read
}
