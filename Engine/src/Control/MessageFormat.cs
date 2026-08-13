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
    /// <summary>Gets the sender user name from <paramref name="message"/>.</summary>
    string GetFromUser(object message);
    /// <summary>Sets the sender user name on <paramref name="message"/>.</summary>
    void SetFromUser(object message, string value);
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
    /// <summary>
    /// Gets the message ID this message is a user-read confirmation for, or an empty string if
    /// <paramref name="message"/> is not a confirmation. A confirmation message carries only this field
    /// (plus <see cref="GetMessageId"/>/<see cref="GetFromUser"/> for its own transport) — subject, body,
    /// and addresses are left unset — and is sent back to the original sender when the recipient opens the
    /// referenced message, so the sender can advance that message's delivery status to <c>Read</c>. See
    /// <c>Docs/Peer.md</c>.
    /// </summary>
    string GetConfirmationMessageId(object message);
    /// <summary>Sets the message ID <paramref name="message"/> is a user-read confirmation for.</summary>
    void SetConfirmationMessageId(object message, string value);
    /// <summary>
    /// Gets whether <paramref name="message"/> is an alert: an ordinary message that also causes the
    /// receiving Client-mode UI to alarm (visually and audibly) until the user reads it. See
    /// <c>Docs/ViewModels.md</c>.
    /// </summary>
    bool GetIsAlert(object message);
    /// <summary>Sets whether <paramref name="message"/> is an alert.</summary>
    void SetIsAlert(object message, bool value);
    /// <summary>
    /// Gets the priority number of <paramref name="message"/>. One of the values returned by
    /// <see cref="IMessagePriorityProvider.GetPriorities"/>; used verbatim as the OFT send priority
    /// (larger values are sent first — see <c>Docs/Peer.md</c>) whenever this message is sent over an
    /// OFT connection.
    /// </summary>
    int GetPriority(object message);
    /// <summary>Sets the priority number on <paramref name="message"/>.</summary>
    void SetPriority(object message, int value);
}

/// <summary>
/// Base class for implementing <see cref="IMessageFormat"/> against a concrete message type <typeparamref name="TMessage"/>.
/// Implements the <c>object</c>-typed <see cref="IMessageFormat"/> members explicitly, casting to
/// <typeparamref name="TMessage"/> once on your behalf, and exposes type-safe <c>protected abstract</c>
/// members instead — derived classes never see or write an <c>object</c>-to-<typeparamref name="TMessage"/>
/// cast. See <c>Sample/src/SampleMessageFormat.cs</c> for a working example.
/// </summary>
/// <typeparam name="TMessage">
/// The concrete message type. Must be protobuf-net serializable (carry <c>[ProtoContract]</c>/<c>[ProtoMember]</c>
/// attributes) for wire transport, LiteDB-serializable for storage, and have a public parameterless
/// constructor (used by the default <see cref="CreateMessage"/> implementation).
/// </typeparam>
public abstract class MessageFormat<TMessage> : IMessageFormat where TMessage : class, new()
{
    /// <inheritdoc cref="IMessageFormat.MessageType" />
    public Type MessageType => typeof(TMessage);

    /// <summary>Creates a new, empty <typeparamref name="TMessage"/>. The default implementation returns <c>new TMessage()</c>; override for custom construction.</summary>
    protected virtual TMessage CreateMessage() => new();
    /// <summary>Gets the application-level message identifier from <paramref name="message"/>.</summary>
    protected abstract string GetMessageId(TMessage message);
    /// <summary>Sets the application-level message identifier on <paramref name="message"/>.</summary>
    protected abstract void SetMessageId(TMessage message, string value);
    /// <summary>Gets the sender user name from <paramref name="message"/>.</summary>
    protected abstract string GetFromUser(TMessage message);
    /// <summary>Sets the sender user name on <paramref name="message"/>.</summary>
    protected abstract void SetFromUser(TMessage message, string value);
    /// <summary>Gets the subject line from <paramref name="message"/>.</summary>
    protected abstract string GetSubject(TMessage message);
    /// <summary>Sets the subject line on <paramref name="message"/>.</summary>
    protected abstract void SetSubject(TMessage message, string value);
    /// <summary>Gets the body text from <paramref name="message"/>.</summary>
    protected abstract string GetBody(TMessage message);
    /// <summary>Sets the body text on <paramref name="message"/>.</summary>
    protected abstract void SetBody(TMessage message, string value);
    /// <summary>Gets the recipient address list from <paramref name="message"/>.</summary>
    protected abstract List<MessageAddress> GetAddresses(TMessage message);
    /// <summary>Sets the recipient address list on <paramref name="message"/>.</summary>
    protected abstract void SetAddresses(TMessage message, List<MessageAddress> value);
    /// <summary>Gets the UTC sent timestamp from <paramref name="message"/>.</summary>
    protected abstract DateTime GetSentAt(TMessage message);
    /// <summary>Sets the UTC sent timestamp on <paramref name="message"/>.</summary>
    protected abstract void SetSentAt(TMessage message, DateTime value);
    /// <summary>Gets the message ID <paramref name="message"/> is a user-read confirmation for, or an empty string if it is not a confirmation.</summary>
    protected abstract string GetConfirmationMessageId(TMessage message);
    /// <summary>Sets the message ID <paramref name="message"/> is a user-read confirmation for.</summary>
    protected abstract void SetConfirmationMessageId(TMessage message, string value);
    /// <summary>Gets whether <paramref name="message"/> is an alert.</summary>
    protected abstract bool GetIsAlert(TMessage message);
    /// <summary>Sets whether <paramref name="message"/> is an alert.</summary>
    protected abstract void SetIsAlert(TMessage message, bool value);
    /// <summary>Gets the priority number of <paramref name="message"/>.</summary>
    protected abstract int GetPriority(TMessage message);
    /// <summary>Sets the priority number on <paramref name="message"/>.</summary>
    protected abstract void SetPriority(TMessage message, int value);

    object IMessageFormat.CreateMessage() => CreateMessage();
    string IMessageFormat.GetMessageId(object message) => GetMessageId((TMessage)message);
    void IMessageFormat.SetMessageId(object message, string value) => SetMessageId((TMessage)message, value);
    string IMessageFormat.GetFromUser(object message) => GetFromUser((TMessage)message);
    void IMessageFormat.SetFromUser(object message, string value) => SetFromUser((TMessage)message, value);
    string IMessageFormat.GetSubject(object message) => GetSubject((TMessage)message);
    void IMessageFormat.SetSubject(object message, string value) => SetSubject((TMessage)message, value);
    string IMessageFormat.GetBody(object message) => GetBody((TMessage)message);
    void IMessageFormat.SetBody(object message, string value) => SetBody((TMessage)message, value);
    List<MessageAddress> IMessageFormat.GetAddresses(object message) => GetAddresses((TMessage)message);
    void IMessageFormat.SetAddresses(object message, List<MessageAddress> value) => SetAddresses((TMessage)message, value);
    DateTime IMessageFormat.GetSentAt(object message) => GetSentAt((TMessage)message);
    void IMessageFormat.SetSentAt(object message, DateTime value) => SetSentAt((TMessage)message, value);
    string IMessageFormat.GetConfirmationMessageId(object message) => GetConfirmationMessageId((TMessage)message);
    void IMessageFormat.SetConfirmationMessageId(object message, string value) => SetConfirmationMessageId((TMessage)message, value);
    bool IMessageFormat.GetIsAlert(object message) => GetIsAlert((TMessage)message);
    void IMessageFormat.SetIsAlert(object message, bool value) => SetIsAlert((TMessage)message, value);
    int IMessageFormat.GetPriority(object message) => GetPriority((TMessage)message);
    void IMessageFormat.SetPriority(object message, int value) => SetPriority((TMessage)message, value);
}
