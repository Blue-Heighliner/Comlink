namespace BlueHeighliner.Comlink.Control;

/// <summary>The engine-side form of a message mapping: every accessor takes the message as an <see cref="object"/>, since the engine works with the host's message type only through <see cref="IEngineController"/>.</summary>
internal sealed class MessageMap
{
    /// <summary>The host's message type.</summary>
    public required Type Type { get; init; }
    /// <summary>The serializer for the message type.</summary>
    public required INetworkSerializer Serializer { get; init; }
    /// <summary>Creates a new, empty message.</summary>
    public required Func<object> Create { get; init; }
    /// <summary>Reads the message identifier.</summary>
    public required Func<object, string> GetId { get; init; }
    /// <summary>Writes the message identifier.</summary>
    public required Action<object, string> SetId { get; init; }
    /// <summary>Reads the sender.</summary>
    public required Func<object, string> GetSender { get; init; }
    /// <summary>Writes the sender.</summary>
    public required Action<object, string> SetSender { get; init; }
    /// <summary>Reads the subject.</summary>
    public required Func<object, string> GetSubject { get; init; }
    /// <summary>Writes the subject.</summary>
    public required Action<object, string> SetSubject { get; init; }
    /// <summary>Reads the body.</summary>
    public required Func<object, string> GetBody { get; init; }
    /// <summary>Writes the body.</summary>
    public required Action<object, string> SetBody { get; init; }
    /// <summary>Reads the recipients.</summary>
    public required Func<object, List<MessageAddress>> GetAddresses { get; init; }
    /// <summary>Writes the recipients.</summary>
    public required Action<object, List<MessageAddress>> SetAddresses { get; init; }
    /// <summary>Reads the sent time.</summary>
    public required Func<object, DateTime> GetSentAt { get; init; }
    /// <summary>Writes the sent time.</summary>
    public required Action<object, DateTime> SetSentAt { get; init; }
    /// <summary>Reads the identifier of the message this one confirms.</summary>
    public required Func<object, string> GetConfirmationId { get; init; }
    /// <summary>Writes the identifier of the message this one confirms.</summary>
    public required Action<object, string> SetConfirmationId { get; init; }
    /// <summary>Reads whether the message is an alert.</summary>
    public required Func<object, bool> GetIsAlert { get; init; }
    /// <summary>Writes whether the message is an alert.</summary>
    public required Action<object, bool> SetIsAlert { get; init; }
    /// <summary>Reads the priority.</summary>
    public required Func<object, int> GetPriority { get; init; }
    /// <summary>Writes the priority.</summary>
    public required Action<object, int> SetPriority { get; init; }
    /// <summary>Reads the tag.</summary>
    public required Func<object, string> GetTag { get; init; }
    /// <summary>Writes the tag.</summary>
    public required Action<object, string> SetTag { get; init; }
    /// <summary>Reads the security level.</summary>
    public required Func<object, string> GetSecurityLevel { get; init; }
    /// <summary>Writes the security level.</summary>
    public required Action<object, string> SetSecurityLevel { get; init; }
}
