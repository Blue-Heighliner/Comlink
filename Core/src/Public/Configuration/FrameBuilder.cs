namespace BlueHeighliner.Comlink;

/// <summary>
/// Maps the engine's logical frame fields onto the fields of the host's own frame type
/// <typeparamref name="TFrame"/>. Every field must be mapped; the engine never assumes any particular field name or
/// shape, and has no frame type of its own. Each mapping is a getter and a setter, so the frame can be read and
/// built without reflection. See <see cref="IEngineBuilder.Frames{TFrame}"/>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface IFrameBuilder<TFrame> where TFrame : class, new()
{
    /// <summary>Maps the application-level frame identifier.</summary>
    IFrameBuilder<TFrame> Id(Func<TFrame, string> get, Action<TFrame, string> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.Id</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IFrameBuilder<TFrame> Id(Expression<Func<TFrame, string>> property);

    /// <summary>Maps the sender's user name.</summary>
    IFrameBuilder<TFrame> Sender(Func<TFrame, string> get, Action<TFrame, string> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.Sender</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IFrameBuilder<TFrame> Sender(Expression<Func<TFrame, string>> property);

    /// <summary>Maps the subject line.</summary>
    IFrameBuilder<TFrame> Subject(Func<TFrame, string> get, Action<TFrame, string> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.Subject</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IFrameBuilder<TFrame> Subject(Expression<Func<TFrame, string>> property);

    /// <summary>Maps the body text.</summary>
    IFrameBuilder<TFrame> Body(Func<TFrame, string> get, Action<TFrame, string> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.Body</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IFrameBuilder<TFrame> Body(Expression<Func<TFrame, string>> property);

    /// <summary>
    /// Maps the recipient list, converting between the host's own recipient shape and the engine's: a name, whether it is
    /// addressed to (<see cref="AddressType.To"/>), copied (<see cref="AddressType.Cc"/>) or outside the system
    /// (<see cref="AddressType.External"/>, information for the user that the engine takes no action for), and any custom
    /// instructions attached to it (for example <c>Deliver to Eastside Office</c>, an empty string when there are none).
    /// The getter is called whenever the engine needs to know who a frame is for, and the setter when it builds a frame.
    /// </summary>
    IFrameBuilder<TFrame> Addresses(Func<TFrame, IEnumerable<(string Name, AddressType Type, string Information)>> get, Action<TFrame, IReadOnlyList<(string Name, AddressType Type, string Information)>> set);

    /// <summary>
    /// Maps the recipient list the same way as the other <c>Addresses</c> overload, for a host whose own recipient
    /// shape has no place for custom instructions; every address maps with an empty <c>Information</c>.
    /// </summary>
    IFrameBuilder<TFrame> Addresses(Func<TFrame, IEnumerable<(string Name, AddressType Type)>> get, Action<TFrame, IReadOnlyList<(string Name, AddressType Type)>> set);

    /// <summary>Maps the UTC time the frame was sent.</summary>
    IFrameBuilder<TFrame> SentAt(Func<TFrame, DateTime> get, Action<TFrame, DateTime> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.SentAt</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IFrameBuilder<TFrame> SentAt(Expression<Func<TFrame, DateTime>> property);

    /// <summary>
    /// Maps the identifier of the message this frame is a user-read confirmation for, an empty string when it is not
    /// a confirmation. A confirmation carries only this field, plus the identifier and sender.
    /// </summary>
    IFrameBuilder<TFrame> ConfirmationId(Func<TFrame, string> get, Action<TFrame, string> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.ConfirmationId</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IFrameBuilder<TFrame> ConfirmationId(Expression<Func<TFrame, string>> property);

    /// <summary>
    /// Maps the fields of a retrieval request: whether the frame is one, and the date range, authors, destinations and
    /// message identifiers it asks a storage server (see <see cref="UserInfo.StoresMessages"/>) for. Each is its
    /// own property of the host's frame type, mapped through <see cref="IRetrievalBuilder{TFrame}"/>; every one must
    /// be mapped.
    /// </summary>
    IFrameBuilder<TFrame> Retrieval(Action<IRetrievalBuilder<TFrame>> map);

    /// <summary>
    /// Maps whether the frame is a message: one the user reads, which is stored in the Inbox when received and in the Outbox when sent by the user. A frame that is not a message
    /// is still routed and handed to the network processor, but is never shown to the user or stored. The engine sets this on every frame it builds, true for
    /// the messages users send and false for its own heartbeats, confirmations and retrieval requests; a host sets it on a frame it sends through the network processor.
    /// </summary>
    IFrameBuilder<TFrame> IsMessage(Func<TFrame, bool> get, Action<TFrame, bool> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.IsMessage</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IFrameBuilder<TFrame> IsMessage(Expression<Func<TFrame, bool>> property);

    /// <summary>Maps whether the message is an alert, which alarms the receiving user interface until it is read.</summary>
    IFrameBuilder<TFrame> IsAlert(Func<TFrame, bool> get, Action<TFrame, bool> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.IsAlert</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IFrameBuilder<TFrame> IsAlert(Expression<Func<TFrame, bool>> property);

    /// <summary>Maps the priority number, one of the values given to <see cref="IEngineBuilder.Priorities"/>, which is also the send priority on the network.</summary>
    IFrameBuilder<TFrame> Priority(Func<TFrame, int> get, Action<TFrame, int> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.Priority</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IFrameBuilder<TFrame> Priority(Expression<Func<TFrame, int>> property);

    /// <summary>Maps the short tag the user gives a message, an empty string when there is none.</summary>
    IFrameBuilder<TFrame> Tag(Func<TFrame, string> get, Action<TFrame, string> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.Tag</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IFrameBuilder<TFrame> Tag(Expression<Func<TFrame, string>> property);

    /// <summary>
    /// Maps the security level this message was sent at, one of the names given to <see cref="IEngineBuilder.SecurityLevels"/>,
    /// or an empty string when no security levels are configured. A destination user whose own assigned level
    /// (see <see cref="UserInfo.SecurityLevel"/>) ranks lower is never sent this message.
    /// </summary>
    IFrameBuilder<TFrame> SecurityLevel(Func<TFrame, string> get, Action<TFrame, string> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.SecurityLevel</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IFrameBuilder<TFrame> SecurityLevel(Expression<Func<TFrame, string>> property);

    /// <summary>
    /// Replaces the serializer that turns frames into the bytes sent across the network. The default is a
    /// <see cref="ProtobufSerializer"/> that builds only <typeparamref name="TFrame"/>, so the frame type then
    /// needs <c>[ProtoContract]</c>/<c>[ProtoMember]</c> attributes. Every node on a network must use a matching serializer.
    /// </summary>
    /// <typeparam name="TSerializer">The serializer type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IFrameBuilder<TFrame> Serializer<TSerializer>() where TSerializer : IFrameSerializer;

    /// <summary>Replaces how a new, empty frame is created. The default is <c>new TFrame()</c>.</summary>
    IFrameBuilder<TFrame> Create(Func<TFrame> create);

    /// <summary>Sets how many copies of a received message are printed while "print received" is on. Defaults to one for every message.</summary>
    IFrameBuilder<TFrame> PrintCount(Func<TFrame, int> copies);

    /// <summary>
    /// Adds a custom auto forward controller, shown as an option in the client's auto forward screen to every user
    /// named in <paramref name="users"/>. Any of them can open it there and maintain their own locally-saved target
    /// list (added to and removed from freely, persisted between restarts); whenever this instance receives a
    /// message that <paramref name="filter"/> accepts, it is automatically forwarded, unchanged in subject and
    /// body, to every user currently on that target list - no action needed beyond having set the target list up
    /// once. <paramref name="filter"/> is never consulted for a user with no access, or with an empty target list,
    /// so an inaccessible or unconfigured controller costs nothing per received message beyond that one check.
    /// Calling this again with the same <paramref name="name"/> (case-insensitive) replaces the earlier controller
    /// of that name in place; a new name adds another alongside it.
    /// </summary>
    /// <param name="name">Display name shown for this controller in the auto forward screen.</param>
    /// <param name="users">User names allowed to open this controller and maintain its target list.</param>
    /// <param name="filter">Answers whether a received message should be auto-forwarded through this controller.</param>
    IFrameBuilder<TFrame> AutoForward(string name, IEnumerable<string> users, Func<TFrame, bool> filter);

    /// <summary>
    /// States the processor that runs host code in reaction to peer activity: a user connecting or disconnecting and a frame being received
    /// (see <see cref="INetworkProcessor{TFrame}"/>). None by default.
    /// </summary>
    /// <typeparam name="TProcessor">The processor type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IFrameBuilder<TFrame> Processor<TProcessor>() where TProcessor : INetworkProcessor<TFrame>;

    /// <summary>
    /// States how nodes introduce themselves on a new connection, with frames: the processor is told when a connection forms and given each frame that
    /// arrives until it marks the connection connected as a named user (see <see cref="IInitialFrameProcessor{TFrame}"/>). What it sends is a serialized
    /// instance of the frame type, split into packets like any frame when packets are configured, and is not stored, routed or shown. Without one, a connection
    /// is identified by <see cref="IEngineBuilder.Identify"/> or the engine's own rule straight away. Every node on a network must be configured alike.
    /// </summary>
    /// <typeparam name="TProcessor">The processor type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IFrameBuilder<TFrame> InitialProcessor<TProcessor>() where TProcessor : IInitialFrameProcessor<TFrame>;
}
