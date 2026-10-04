namespace BlueHeighliner.Comlink;

/// <summary>LiteDB document holding a copy of a message one of a server's children sent.</summary>
internal sealed class StoredMessageEntity
{
    /// <summary>Unique document identifier.</summary>
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    /// <summary>Application-level message identifier, denormalized from <see cref="Message"/> so LiteDB can query and index on it directly.</summary>
    public string MessageId { get; set; } = string.Empty;
    /// <summary>The routed message, as an instance of <see cref="IEngineController.FrameType"/>; read its fields through <see cref="IEngineController"/>.</summary>
    public object Message { get; set; } = default!;
    /// <summary>UTC timestamp when this server stored the copy.</summary>
    public DateTime StoredAt { get; set; } = DateTime.UtcNow;
}
