namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Demonstrates injecting a custom frame DTO. Field names are deliberately unlike those of the engine's
/// <see cref="Message"/> (<c>Id</c>, <c>Sender</c>, <c>Text</c>, <c>Recipients</c> with a <see cref="bool"/> flag vs an address-type enum):
/// the engine never reads a frame, and this type converts to and from <see cref="Message"/> (<see cref="FromMessage"/>, <see cref="ToMessage"/>) for <see cref="NetworkProcessor"/>.
/// </summary>
[ProtoContract]
public sealed class Frame
{
    /// <summary>Creates the frame that carries <paramref name="message"/> over the network.</summary>
    /// <param name="message">The message to carry.</param>
    public static Frame FromMessage(Message message)
        => new()
        {
            IsMessage = true,
            Id = message.Id,
            Sender = message.FromUser,
            Text = message.Body,
            Timestamp = message.SentAt,
            Importance = (int)message.Priority,
            Category = message.Tag,
            Confidentiality = (int?)message.MessageLevel,
            Protection = (int?)message.MessageAspect,
            Recipients = [.. message.Addresses.Select(address => new Recipient { User = address.UserName, Kind = address.Type switch { AddressType.Cc => "CC", AddressType.External => "OUTSIDE", _ => "TO" }, Note = address.Information })]
        };

    /// <summary>Application-level message identifier.</summary>
    [ProtoMember(1)] public string Id { get; set; } = string.Empty;
    /// <summary>User name of the sender.</summary>
    [ProtoMember(2)] public string Sender { get; set; } = string.Empty;
    /// <summary>Message body text.</summary>
    [ProtoMember(4)] public string Text { get; set; } = string.Empty;
    /// <summary>Recipient list.</summary>
    [ProtoMember(5)] public List<Recipient> Recipients { get; set; } = [];
    /// <summary>UTC timestamp when the message was originally sent.</summary>
    [ProtoMember(6)] public DateTime Timestamp { get; set; }
    /// <summary>Message ID this message is a read receipt for; empty for an ordinary message.</summary>
    [ProtoMember(7)] public string ReadMessageId { get; set; } = string.Empty;
    /// <summary>Integer value of the priority level of this message, the value of a MessagePriority member.</summary>
    [ProtoMember(9)] public int Importance { get; set; }
    /// <summary>Short user-inputted tag identifying the type of this message.</summary>
    [ProtoMember(10)] public string Category { get; set; } = string.Empty;
    /// <summary>Integer value of the message level this message was sent at, the value of a MessageLevel member, or <see langword="null"/> for none.</summary>
    [ProtoMember(11)] public int? Confidentiality { get; set; }
    /// <summary>Integer value of the message aspect this message carries, the value of a MessageAspect member, or <see langword="null"/> for none.</summary>
    [ProtoMember(22)] public int? Protection { get; set; }
    /// <summary>Whether this message is a retrieval request to a storage server.</summary>
    [ProtoMember(12)] public bool IsRetrieval { get; set; }
    /// <summary>Retrieval request lower sent-time bound.</summary>
    [ProtoMember(13)] public DateTime? RetrievalFrom { get; set; }
    /// <summary>Retrieval request upper sent-time bound.</summary>
    [ProtoMember(14)] public DateTime? RetrievalTo { get; set; }
    /// <summary>Retrieval request sender names.</summary>
    [ProtoMember(15)] public List<string> RetrievalAuthors { get; set; } = [];
    /// <summary>Retrieval request addressee names.</summary>
    [ProtoMember(16)] public List<string> RetrievalDestinations { get; set; } = [];
    /// <summary>Retrieval request message identifiers.</summary>
    [ProtoMember(17)] public List<string> RetrievalIds { get; set; } = [];
    /// <summary>Whether this frame is a message the user reads and that is stored, rather than only network traffic.</summary>
    [ProtoMember(18)] public bool IsMessage { get; set; }
    /// <summary>Whether this frame is a read receipt for the message in <see cref="ReadMessageId"/>.</summary>
    [ProtoMember(19)] public bool IsReadReceipt { get; set; }
    /// <summary>The message this frame is a receive receipt for, when <see cref="IsReceiveReceipt"/>.</summary>
    [ProtoMember(20)] public string ReceivedMessageId { get; set; } = string.Empty;
    /// <summary>Whether this frame is a receive receipt for the message in <see cref="ReceivedMessageId"/>.</summary>
    [ProtoMember(21)] public bool IsReceiveReceipt { get; set; }

    /// <summary>Gets the priority this frame is sent with: that of a receipt or a retrieval request, or the message's own importance.</summary>
    [ProtoIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public MessagePriority Priority
        => IsReadReceipt || IsReceiveReceipt ? MessagePriority.Receipt
        : IsRetrieval ? MessagePriority.Retrieval
        : (MessagePriority)Importance;

    /// <summary>Turns this frame into the message the engine stores and shows.</summary>
    public Message ToMessage()
        => new()
        {
            Id = Id,
            FromUser = Sender,
            Body = Text,
            SentAt = Timestamp,
            Priority = (MessagePriority)Importance,
            Tag = Category,
            MessageLevel = (MessageLevel?)Confidentiality,
            MessageAspect = (MessageAspect?)Protection,
            IsAlert = Category is "ALERT",
            Addresses = [.. Recipients.Select(recipient => new MessageAddress { UserName = recipient.User, Type = recipient.Kind switch { "CC" => AddressType.Cc, "OUTSIDE" => AddressType.External, _ => AddressType.To }, Information = recipient.Note })]
        };
}

/// <summary>A single recipient entry within a <see cref="Frame"/>.</summary>
[ProtoContract]
public sealed class Recipient
{
    /// <summary>User name of the addressee.</summary>
    [ProtoMember(1)] public string User { get; set; } = string.Empty;
    /// <summary>How the recipient is addressed: <c>TO</c>, <c>CC</c>, or <c>OUTSIDE</c> for an address outside the system.</summary>
    [ProtoMember(2)] public string Kind { get; set; } = "TO";
    /// <summary>Custom instructions attached to the recipient, such as <c>Deliver to Eastside Office</c>.</summary>
    [ProtoMember(3)] public string Note { get; set; } = string.Empty;
}
