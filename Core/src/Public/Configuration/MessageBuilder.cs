namespace BlueHeighliner.Comlink;

/// <summary>
/// Maps the engine's logical message fields onto the fields of the host's own message type
/// <typeparamref name="TMessage"/>. Every field must be mapped; the engine never assumes any particular field name or
/// shape, and has no message type of its own. Each mapping is a getter and a setter, so the message can be read and
/// built without reflection. See <see cref="IEngineBuilder.Message{TMessage}"/>.
/// </summary>
/// <typeparam name="TMessage">The host's message type.</typeparam>
public interface IMessageBuilder<TMessage> where TMessage : class, new()
{
    /// <summary>Maps the application-level message identifier.</summary>
    IMessageBuilder<TMessage> Id(Func<TMessage, string> get, Action<TMessage, string> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.Id</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IMessageBuilder<TMessage> Id(Expression<Func<TMessage, string>> property);

    /// <summary>Maps the sender's user name.</summary>
    IMessageBuilder<TMessage> Sender(Func<TMessage, string> get, Action<TMessage, string> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.Sender</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IMessageBuilder<TMessage> Sender(Expression<Func<TMessage, string>> property);

    /// <summary>Maps the subject line.</summary>
    IMessageBuilder<TMessage> Subject(Func<TMessage, string> get, Action<TMessage, string> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.Subject</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IMessageBuilder<TMessage> Subject(Expression<Func<TMessage, string>> property);

    /// <summary>Maps the body text.</summary>
    IMessageBuilder<TMessage> Body(Func<TMessage, string> get, Action<TMessage, string> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.Body</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IMessageBuilder<TMessage> Body(Expression<Func<TMessage, string>> property);

    /// <summary>
    /// Maps the recipient list, converting between the host's own recipient shape and the engine's: a name, whether it is
    /// addressed to (<see cref="AddressType.To"/>), copied (<see cref="AddressType.Cc"/>) or outside the system
    /// (<see cref="AddressType.External"/>, information for the user that the engine takes no action for), and any custom
    /// instructions attached to it (for example <c>Deliver to Eastside Office</c>, an empty string when there are none).
    /// The getter is called whenever the engine needs to know who a message is for, and the setter when it builds a message.
    /// </summary>
    IMessageBuilder<TMessage> Addresses(Func<TMessage, IEnumerable<(string Name, AddressType Type, string Information)>> get, Action<TMessage, IReadOnlyList<(string Name, AddressType Type, string Information)>> set);

    /// <summary>
    /// Maps the recipient list the same way as the other <c>Addresses</c> overload, for a host whose own recipient
    /// shape has no place for custom instructions; every address maps with an empty <c>Information</c>.
    /// </summary>
    IMessageBuilder<TMessage> Addresses(Func<TMessage, IEnumerable<(string Name, AddressType Type)>> get, Action<TMessage, IReadOnlyList<(string Name, AddressType Type)>> set);

    /// <summary>Maps the UTC time the message was sent.</summary>
    IMessageBuilder<TMessage> SentAt(Func<TMessage, DateTime> get, Action<TMessage, DateTime> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.SentAt</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IMessageBuilder<TMessage> SentAt(Expression<Func<TMessage, DateTime>> property);

    /// <summary>
    /// Maps the identifier of the message this message is a user-read confirmation for, an empty string when it is not
    /// a confirmation. A confirmation carries only this field, plus the identifier and sender.
    /// </summary>
    IMessageBuilder<TMessage> ConfirmationId(Func<TMessage, string> get, Action<TMessage, string> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.ConfirmationId</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IMessageBuilder<TMessage> ConfirmationId(Expression<Func<TMessage, string>> property);

    /// <summary>Maps whether the message is an alert, which alarms the receiving user interface until it is read.</summary>
    IMessageBuilder<TMessage> IsAlert(Func<TMessage, bool> get, Action<TMessage, bool> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.IsAlert</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IMessageBuilder<TMessage> IsAlert(Expression<Func<TMessage, bool>> property);

    /// <summary>Maps the priority number, one of the values given to <see cref="IEngineBuilder.Priorities"/>, which is also the send priority on the network.</summary>
    IMessageBuilder<TMessage> Priority(Func<TMessage, int> get, Action<TMessage, int> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.Priority</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IMessageBuilder<TMessage> Priority(Expression<Func<TMessage, int>> property);

    /// <summary>Maps the short tag the user gives a message, an empty string when there is none.</summary>
    IMessageBuilder<TMessage> Tag(Func<TMessage, string> get, Action<TMessage, string> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.Tag</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IMessageBuilder<TMessage> Tag(Expression<Func<TMessage, string>> property);

    /// <summary>
    /// Maps the security level this message was sent at, one of the names given to <see cref="IEngineBuilder.SecurityLevels"/>,
    /// or an empty string when no security levels are configured. A destination user whose own assigned level
    /// (see <see cref="IEngineBuilder.UserSecurityLevel(string,string)"/>) ranks lower is never sent this message.
    /// </summary>
    IMessageBuilder<TMessage> SecurityLevel(Func<TMessage, string> get, Action<TMessage, string> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.SecurityLevel</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IMessageBuilder<TMessage> SecurityLevel(Expression<Func<TMessage, string>> property);

    /// <summary>
    /// Replaces the serializer that turns messages into the bytes sent across the network. The default is a
    /// <see cref="ProtobufNetworkSerializer"/> that builds only <typeparamref name="TMessage"/>, so the message type then
    /// needs <c>[ProtoContract]</c>/<c>[ProtoMember]</c> attributes. Every node on a network must use a matching serializer.
    /// </summary>
    IMessageBuilder<TMessage> Serializer(INetworkSerializer serializer);

    /// <summary>Replaces how a new, empty message is created. The default is <c>new TMessage()</c>.</summary>
    IMessageBuilder<TMessage> Create(Func<TMessage> create);
}
