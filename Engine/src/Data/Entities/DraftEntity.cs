namespace BlueHeighliner.Comlink.Engine.Data.Entities;

/// <summary>LiteDB document representing a draft message.</summary>
public sealed class DraftEntity
{
    /// <summary>Unique document identifier.</summary>
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    /// <summary>Subject line of the draft.</summary>
    public string Subject { get; set; } = string.Empty;
    /// <summary>Plain-text body of the draft.</summary>
    public string Body { get; set; } = string.Empty;
    /// <summary>JSON-serialized array of <see cref="DraftBodySegmentData"/> segments.</summary>
    public string BodySegmentsJson { get; set; } = string.Empty;
    /// <summary>Recipient addresses associated with this draft.</summary>
    public List<AddressData> Addresses { get; set; } = [];
    /// <summary>UTC timestamp when this draft was first created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>UTC timestamp of the most recent modification.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Indicates whether this draft has been sent.</summary>
    public bool IsSent { get; set; }
    /// <summary>Whether this draft should be sent as an alert; see <see cref="IMessageFormat.GetIsAlert"/>.</summary>
    public bool IsAlert { get; set; }
    /// <summary>Priority number this draft should be sent at; see <see cref="IMessageFormat.GetPriority"/>.</summary>
    public int Priority { get; set; }
    /// <summary>Tag identifying the type of this draft; see <see cref="IMessageFormat.GetTag"/>.</summary>
    public string Tag { get; set; } = string.Empty;
    /// <summary>UTC timestamp when the draft was sent, or <c>null</c> if not yet sent.</summary>
    public DateTime? SentAt { get; set; }
    /// <summary>Identifier of the folder this draft belongs to.</summary>
    public string FolderId { get; set; } = string.Empty;
}
