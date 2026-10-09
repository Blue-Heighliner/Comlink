namespace BlueHeighliner.Comlink;

/// <summary>
/// Describes the packets payloads are broken into for the network. The engine does all of the splitting and
/// reassembling itself; the host only says how a packet is stored and serialized, by stating the packet handler that creates and reads its packets.
/// The handler must be stated. See <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Packets"/>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/>.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects, or <see cref="NoMessageAspect"/> for none.</typeparam>
public interface IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Replaces the serializer that turns packets into bytes. The default is a <see cref="ProtobufSerializer"/> that builds only <typeparamref name="TPacket"/>.</summary>
    /// <typeparam name="TSerializer">The serializer type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Serializer<TSerializer>() where TSerializer : IPacketSerializer;

    /// <summary>
    /// States how nodes introduce themselves on a new connection: the handshake processor is told when a connection forms and given each packet that
    /// arrives until it marks the connection connected as a named user (see <see cref="IPacketHandshakeProcessor{TPacket}"/>). What it sends is a serialized
    /// instance of the packet type sent as it is, beneath the packetizer, before any frame handshake. Every node on a network must be configured alike.
    /// Like every other thing sent between nodes it is a serialized instance of the frame or packet type and nothing else.
    /// </summary>
    /// <typeparam name="TProcessor">The processor type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Handshake<TProcessor>() where TProcessor : IPacketHandshakeProcessor<TPacket>;

    /// <summary>
    /// States the handler for heartbeat packets (see <see cref="IPacketHeartbeatHandler{TPacket, TPriority}"/>): a heartbeat sent as a packet of its own, beneath
    /// packetization, so it is never split or reassembled and its receiver discards it. Optional, and when stated it is used instead of any heartbeat frame stated with
    /// <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Heartbeat{THandler}"/>. Without either no heartbeats are sent. Heartbeats are never sent over HDLC.
    /// </summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Heartbeat<THandler>() where THandler : IPacketHeartbeatHandler<TPacket, TPriority>;

    /// <summary>
    /// Sets how many packets may be in flight over one connection at once. A higher-priority payload sent meanwhile goes out
    /// as soon as the packets in flight finish, so the window is how many it can end up waiting behind: 1 (the
    /// default) is the most responsive, while a wider window keeps a link with a long round trip busier. Must be at least 1.
    /// </summary>
    /// <param name="packets">The number of packets.</param>
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Window(int packets);
}
