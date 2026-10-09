namespace BlueHeighliner.Comlink;

/// <summary>
/// Configures the host's own frame type, continuing the fluent chain of the engine builder: every setting of the engine builder can follow on <typeparamref name="TFrame"/>. The engine
/// never looks inside a frame and has no frame type of its own: what a frame means, which of them are messages, receipts or requests, and who it goes to is the host's handler's (see <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}"/>).
/// What the engine needs of a frame is only how to serialize it, how to create an empty one, and, optionally, which one is a heartbeat. See <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Frames{THandler}"/>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/>.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects, or <see cref="NoMessageAspect"/> for none.</typeparam>
public interface IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>
    /// States the handler for heartbeat frames (see <see cref="IFrameHeartbeatHandler{TFrame, TPriority}"/>), which a node sends over each MSMT connection to verify it is really up and keep it live. Optional: when not
    /// stated no heartbeats are sent, and an MSMT connection counts as up as soon as it is established. Heartbeats are never sent over HDLC.
    /// </summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Heartbeat<THandler>() where THandler : IFrameHeartbeatHandler<TFrame, TPriority>;

    /// <summary>
    /// Replaces the serializer that turns frames into the bytes sent across the network. The default is a
    /// <see cref="ProtobufSerializer"/> that builds only <typeparamref name="TFrame"/>, so the frame type then
    /// needs <c>[ProtoContract]</c>/<c>[ProtoMember]</c> attributes. Every node on a network must use a matching serializer.
    /// </summary>
    /// <typeparam name="TSerializer">The serializer type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Serializer<TSerializer>() where TSerializer : IFrameSerializer;

    /// <summary>Replaces how a new, empty frame is created. The default is <c>new TFrame()</c>.</summary>
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Create(Func<TFrame> create);

    /// <summary>
    /// States how nodes introduce themselves on a new connection, with frames: the handler is told when a connection forms and given each frame that
    /// arrives until it marks the connection connected as a named user (see <see cref="IFrameHandshakeHandler{TFrame}"/>). What it sends is a serialized
    /// instance of the frame type, split into packets like any frame when packets are configured, and is not stored, routed or shown. It runs above packetization, after any
    /// packet handshake. Without one, a connection is identified by the engine's own rule straight away. Every node on a network must be configured alike.
    /// </summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Handshake<THandler>() where THandler : IFrameHandshakeHandler<TFrame>;
}
