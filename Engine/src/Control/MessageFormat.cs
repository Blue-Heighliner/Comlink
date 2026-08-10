namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>
/// Provides the concrete message type used to represent a message throughout the engine — the type
/// transmitted between peers and interfaces, and the type stored in the database (see
/// <see cref="MessageType"/>) — and maps the engine's logical fields (message id, sender, subject,
/// body, addresses, sent time) onto that type's real fields. The engine has no built-in message type
/// of its own; a host must register an implementation before starting the engine.
/// </summary>
public interface IMessageFormat
{
    /// <summary>
    /// The concrete message type used throughout the engine. Must be protobuf-net serializable (carry
    /// <c>[ProtoContract]</c>/<c>[ProtoMember]</c> attributes) for wire transport, and must be a type
    /// LiteDB can serialize for storage.
    /// </summary>
    Type MessageType { get; }
    /// <summary>Creates a new, empty instance of <see cref="MessageType"/>.</summary>
    object CreateMessage();
    /// <summary>Gets the application-level message identifier from <paramref name="message"/>.</summary>
    string GetMessageId(object message);
    /// <summary>Sets the application-level message identifier on <paramref name="message"/>.</summary>
    void SetMessageId(object message, string value);
    /// <summary>Gets the sender site name from <paramref name="message"/>.</summary>
    string GetFromSite(object message);
    /// <summary>Sets the sender site name on <paramref name="message"/>.</summary>
    void SetFromSite(object message, string value);
    /// <summary>Gets the subject line from <paramref name="message"/>.</summary>
    string GetSubject(object message);
    /// <summary>Sets the subject line on <paramref name="message"/>.</summary>
    void SetSubject(object message, string value);
    /// <summary>Gets the body text from <paramref name="message"/>.</summary>
    string GetBody(object message);
    /// <summary>Sets the body text on <paramref name="message"/>.</summary>
    void SetBody(object message, string value);
    /// <summary>Gets the recipient address list from <paramref name="message"/>.</summary>
    List<MessageAddress> GetAddresses(object message);
    /// <summary>Sets the recipient address list on <paramref name="message"/>.</summary>
    void SetAddresses(object message, List<MessageAddress> value);
    /// <summary>Gets the UTC sent timestamp from <paramref name="message"/>.</summary>
    DateTime GetSentAt(object message);
    /// <summary>Sets the UTC sent timestamp on <paramref name="message"/>.</summary>
    void SetSentAt(object message, DateTime value);
}
