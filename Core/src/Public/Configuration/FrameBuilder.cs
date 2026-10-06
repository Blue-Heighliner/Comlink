namespace BlueHeighliner.Comlink;

/// <summary>
/// Configures the host's own frame type, continuing the fluent chain of the engine builder: every setting of the engine builder can follow on <typeparamref name="TFrame"/>. The fields every frame has (identifier, sender, addresses, sent time) are mapped with a getter and
/// a setter, and each kind of frame (message, retrieval request, read receipt, receive receipt) is stated with a handler that creates, recognizes and reads that kind.
/// Every one must be stated; the engine never assumes any particular field name or shape, and has no frame type of its own. See <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel}.Frames"/>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/>.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the security levels.</typeparam>
public interface IFrameBuilder<TFrame, TPacket, TPriority, TLevel> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum
{
    /// <summary>
    /// States the handler for message frames: the ones the user reads, which are stored in the Inbox when received and in the Outbox when sent by the user. The handler creates a
    /// message from its content, recognizes message frames and reads their content (see <see cref="IMessageHandler{TFrame, TPriority, TLevel}"/>). A frame that is not a message
    /// is still routed and handed to the network processor, but is never shown to the user or stored.
    /// </summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel> Message<THandler>() where THandler : IMessageHandler<TFrame, TPriority, TLevel>;

    /// <summary>
    /// Adds a custom auto forward controller (see <see cref="IAutoForwardController{TFrame}"/>), shown as an option in the client's auto forward screen to the users it names. Only messages
    /// are forwarded. Adding another controller with the same name (case-insensitive) replaces the earlier one in place; a new name adds another alongside it.
    /// </summary>
    /// <typeparam name="TController">The controller type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel> AutoForward<TController>() where TController : IAutoForwardController<TFrame>;

    /// <summary>
    /// States the handler for retrieval request frames, what a user sends a server to ask for stored messages
    /// (see <see cref="IRetrievalHandler{TFrame, TPriority}"/>). A request is never shown to a user as a received message and is not a message.
    /// </summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel> Retrieval<THandler>() where THandler : IRetrievalHandler<TFrame, TPriority>;

    /// <summary>
    /// States the handler for read receipt frames, sent back to the sender of a message when its recipient opens it (see <see cref="IReadReceiptHandler{TFrame, TPriority}"/>).
    /// A receipt carries only the identifier of the message it is for plus the frame's own identifier and sender, and is not a message.
    /// </summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel> ReadReceipt<THandler>() where THandler : IReadReceiptHandler<TFrame, TPriority>;

    /// <summary>
    /// States the handler for receive receipt frames, sent back to the sender of a message as soon as its recipient's node receives it (see <see cref="IReceiveReceiptHandler{TFrame, TPriority}"/>).
    /// A receipt carries only the identifier of the message it is for plus the frame's own identifier and sender, and is not a message.
    /// </summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel> ReceiveReceipt<THandler>() where THandler : IReceiveReceiptHandler<TFrame, TPriority>;

    /// <summary>
    /// States the handler for heartbeat frames (see <see cref="IHeartbeatHandler{TFrame, TPriority}"/>), which a node sends over each MSMT connection to verify it is really up and keep it live. Optional: when not
    /// stated no heartbeats are sent, and an MSMT connection counts as up as soon as it is established. Heartbeats are never sent over HDLC.
    /// </summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel> Heartbeat<THandler>() where THandler : IHeartbeatHandler<TFrame, TPriority>;

    /// <summary>
    /// Replaces the serializer that turns frames into the bytes sent across the network. The default is a
    /// <see cref="ProtobufSerializer"/> that builds only <typeparamref name="TFrame"/>, so the frame type then
    /// needs <c>[ProtoContract]</c>/<c>[ProtoMember]</c> attributes. Every node on a network must use a matching serializer.
    /// </summary>
    /// <typeparam name="TSerializer">The serializer type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel> Serializer<TSerializer>() where TSerializer : IFrameSerializer;

    /// <summary>Replaces how a new, empty frame is created. The default is <c>new TFrame()</c>.</summary>
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel> Create(Func<TFrame> create);

    /// <summary>
    /// States the processor that runs host code in reaction to peer activity: a user connecting or disconnecting and a frame being received
    /// (see <see cref="INetworkProcessor{TFrame}"/>). None by default.
    /// </summary>
    /// <typeparam name="TProcessor">The processor type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel> Processor<TProcessor>() where TProcessor : INetworkProcessor<TFrame>;

    /// <summary>
    /// States how nodes introduce themselves on a new connection, with frames: the processor is told when a connection forms and given each frame that
    /// arrives until it marks the connection connected as a named user (see <see cref="IInitialFrameProcessor{TFrame}"/>). What it sends is a serialized
    /// instance of the frame type, split into packets like any frame when packets are configured, and is not stored, routed or shown. Without one, a connection
    /// is identified by the engine's own rule straight away. Every node on a network must be configured alike.
    /// </summary>
    /// <typeparam name="TProcessor">The processor type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel> InitialProcessor<TProcessor>() where TProcessor : IInitialFrameProcessor<TFrame>;
}
