namespace BlueHeighliner.Comlink.Engine.Data.Entities;

/// <summary>LiteDB document representing a received or sent message.</summary>
public sealed class MessageEntity
{
    /// <summary>Unique document identifier.</summary>
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    /// <summary>
    /// Application-level message identifier shared with peers. Denormalized from <see cref="Message"/>
    /// (via <see cref="IEngineController.GetMessageId"/>) so LiteDB can query and index on it directly,
    /// since <see cref="Message"/>'s concrete shape is chosen by the host and not known to LiteDB's typed API.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;
    /// <summary>
    /// The message content — subject, body, sender, addresses, sent time — as an instance of
    /// <see cref="IEngineController.MessageType"/>. This is the canonical representation of the message;
    /// read its logical fields via the registered <see cref="IEngineController"/>.
    /// </summary>
    public object Message { get; set; } = default!;
    /// <summary>Per-user delivery statuses for outbound messages.</summary>
    public List<DeliveryStatus> DeliveryStatuses { get; set; } = [];
    /// <summary>UTC timestamp when the message was received.</summary>
    public DateTime ReceivedAt { get; set; }
    /// <summary>UTC timestamp when this document was first created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Identifier of the folder this message belongs to.</summary>
    public string FolderId { get; set; } = string.Empty;
    /// <summary>
    /// <see langword="true"/> when this document is the Outbox record for a message sent by this user;
    /// <see langword="false"/> when it is the Inbox record for a message received by this user. A self-addressed
    /// message produces one document of each kind sharing the same <see cref="MessageId"/>, so this flag
    /// disambiguates lookups that would otherwise be ambiguous.
    /// </summary>
    public bool IsOutbound { get; set; }
    /// <summary>
    /// Inbox-only read status: <see cref="DestinationStatus.Received"/> when stored, <see cref="DestinationStatus.Read"/>
    /// once the user opens it (which also sends a user-read confirmation message back to <see cref="IEngineController.GetFromUser"/>
    /// — see <c>Docs/Peer.md</c>). Always <see langword="null"/> on Outbox records; per-destination read state
    /// there lives in <see cref="DeliveryStatuses"/> instead.
    /// </summary>
    public DestinationStatus? ReadStatus { get; set; }

    /// <summary>Computed aggregate delivery status derived from all per-user statuses; <c>null</c> if no statuses exist.</summary>
    [BsonIgnore]
    public DestinationStatus? OverallStatus
    {
        get
        {
            if (DeliveryStatuses.Count == 0) { return null; }
            if (DeliveryStatuses.Any(d => d.Status == DestinationStatus.Failed)) { return DestinationStatus.Failed; }
            if (DeliveryStatuses.All(d => d.Status == DestinationStatus.Read)) { return DestinationStatus.Read; }
            if (DeliveryStatuses.All(d => d.Status is DestinationStatus.Confirmed or DestinationStatus.Read)) { return DestinationStatus.Confirmed; }
            if (DeliveryStatuses.All(d => d.Status != DestinationStatus.Sending)) { return DestinationStatus.Sent; }
            return DestinationStatus.Sending;
        }
    }
}
