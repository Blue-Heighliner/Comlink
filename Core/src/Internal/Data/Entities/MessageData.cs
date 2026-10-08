namespace BlueHeighliner.Comlink;

/// <summary>The stored form of a <see cref="Message"/>: what a message document keeps. The host's enums are stored as their integer values, which never change for a member, and the engine turns them back into members when it reads one (see <see cref="MessageMapping"/>).</summary>
internal sealed class MessageData
{
    /// <summary>The identifier of the message, shared with its peers.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The name of the sender.</summary>
    public string FromUser { get; set; } = string.Empty;

    /// <summary>The body text.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>The addresses the message is for, groups unexpanded.</summary>
    public List<AddressData> Addresses { get; set; } = [];

    /// <summary>The UTC time the message was sent.</summary>
    public DateTime SentAt { get; set; }

    /// <summary>The integer value of the member of the host's priority enum that is the message's priority.</summary>
    public int Priority { get; set; }

    /// <summary>The short tag identifying the type of message, or an empty string for none.</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>The integer value of the member of the host's message level enum that is the message's level, or <see langword="null"/> for none.</summary>
    public int? MessageLevel { get; set; }

    /// <summary>The integer value of the member of the host's message aspect enum that is the message's aspect, or <see langword="null"/> for none.</summary>
    public int? MessageAspect { get; set; }

    /// <summary>Whether the message is an alert.</summary>
    public bool IsAlert { get; set; }
}
