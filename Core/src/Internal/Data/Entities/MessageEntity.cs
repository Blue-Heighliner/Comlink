namespace BlueHeighliner.Comlink;

/// <summary>LiteDB document representing a received or sent message.</summary>
internal sealed class MessageEntity
{
    /// <summary>Unique document identifier.</summary>
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    /// <summary>Application-level message identifier shared with peers, denormalized from <see cref="Message"/> so LiteDB can query and index on it directly.</summary>
    public string MessageId { get; set; } = string.Empty;
    /// <summary>The message content: body, sender, addresses, sent time, priority, tag, message level, message aspect and whether it is an alert. This is the canonical representation of the message.</summary>
    public MessageData Message { get; set; } = new();
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
    /// once the user opens it (which also tells the host's frame handler, see <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}.OnRead"/>). Always <see langword="null"/> on Outbox records; per-destination read state
    /// there lives in <see cref="DeliveryStatuses"/> instead.
    /// </summary>
    public DestinationStatus? ReadStatus { get; set; }

    /// <summary>Computed aggregate delivery status derived from all per-user statuses; <c>null</c> if no statuses exist.</summary>
    [BsonIgnore]
    public DestinationStatus? OverallStatus
    {
        get
        {
            if (DeliveryStatuses.Count == 0)
            {
                return null;
            }
            if (DeliveryStatuses.Any(d => d.Status is DestinationStatus.Failed))
            {
                return DestinationStatus.Failed;
            }
            if (DeliveryStatuses.All(d => d.Status is DestinationStatus.Read))
            {
                return DestinationStatus.Read;
            }
            if (DeliveryStatuses.All(d => d.Status is DestinationStatus.Received or DestinationStatus.Read))
            {
                return DestinationStatus.Received;
            }
            if (DeliveryStatuses.All(d => d.Status is not DestinationStatus.Sending))
            {
                return DestinationStatus.Sent;
            }
            return DestinationStatus.Sending;
        }
    }
}
