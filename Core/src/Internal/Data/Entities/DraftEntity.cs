namespace BlueHeighliner.Comlink;

/// <summary>LiteDB document representing a draft message.</summary>
internal sealed class DraftEntity
{
    /// <summary>Unique document identifier.</summary>
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    /// <summary>The name the user gave the draft, or <c>null</c> to be named by the first line of its body.</summary>
    public string? Name { get; set; }
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
    /// <summary>Whether this draft should be sent as an alert.</summary>
    public bool IsAlert { get; set; }
    /// <summary>Integer value of the member of the host's priority enum this draft should be sent at.</summary>
    public int Priority { get; set; }
    /// <summary>Tag identifying the type of this draft.</summary>
    public string Tag { get; set; } = string.Empty;
    /// <summary>Integer value of the member of the host's message level enum this draft should be sent at, or <see langword="null"/> for none.</summary>
    public int? MessageLevel { get; set; }
    /// <summary>Value of the message aspect this draft should be sent with, or <see langword="null"/> for none; see <see cref="IEngineController.MessageAspects"/>.</summary>
    public int? MessageAspect { get; set; }
    /// <summary>How many monospace characters wide a line of this draft is set to be, or <c>null</c> for no limit.</summary>
    public int? LineWidth { get; set; }
    /// <summary>UTC timestamp when the draft was sent, or <c>null</c> if not yet sent.</summary>
    public DateTime? SentAt { get; set; }
    /// <summary>Identifier of the folder this draft belongs to.</summary>
    public string FolderId { get; set; } = string.Empty;
}
