namespace BlueHeighliner.Comlink;

/// <summary>LiteDB document holding a copy of a message a server's handler chose to keep for retrievals.</summary>
internal sealed class StoredMessageEntity
{
    /// <summary>Unique document identifier.</summary>
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    /// <summary>Application-level message identifier, denormalized from <see cref="Message"/> so LiteDB can query and index on it directly.</summary>
    public string MessageId { get; set; } = string.Empty;
    /// <summary>The kept message.</summary>
    public MessageData Message { get; set; } = new();
    /// <summary>UTC timestamp when this server stored the copy.</summary>
    public DateTime StoredAt { get; set; } = DateTime.UtcNow;
}
