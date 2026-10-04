namespace BlueHeighliner.Comlink;

/// <summary>
/// Describes the packets payloads are broken into for the network. The engine does all of the splitting and
/// reassembling itself; the host only says how a packet is stored and serialized, by stating the handler that creates and reads its frame packets.
/// The handler must be stated. See <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel}.Packets"/>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/>.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the security levels.</typeparam>
public interface IPacketBuilder<TFrame, TPacket, TPriority, TLevel> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum
{
    /// <summary>
    /// States the handler for frame packets: the packets that carry a piece of a serialized frame. The handler creates a frame packet from a piece of a payload, recognizes frame packets
    /// and reads their aspects back (see <see cref="IFramePacketHandler{TPacket}"/>). The engine creates every packet it cuts a payload into through it and calls
    /// <see cref="IFrameSerializer.ConfigurePacket"/> on each with its frame, so a frame packet can have its own properties set from the frame.
    /// </summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel> Frame<THandler>() where THandler : IFramePacketHandler<TPacket>;

    /// <summary>Replaces the serializer that turns packets into bytes. The default is a <see cref="ProtobufSerializer"/> that builds only <typeparamref name="TPacket"/>.</summary>
    /// <typeparam name="TSerializer">The serializer type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel> Serializer<TSerializer>() where TSerializer : IPacketSerializer;

    /// <summary>
    /// States how nodes introduce themselves on a new connection, with packets: the processor is told when a connection forms and given each packet that
    /// arrives until it marks the connection connected as a named user (see <see cref="IInitialPacketProcessor{TPacket}"/>). What it sends is a serialized
    /// instance of the packet type sent as it is, beneath the packetizer and before any initial message exchange. Every node on a network must be configured alike.
    /// Like every other thing sent between nodes it is a serialized instance of the frame or packet type and nothing else.
    /// </summary>
    /// <typeparam name="TProcessor">The processor type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel> InitialProcessor<TProcessor>() where TProcessor : IInitialPacketProcessor<TPacket>;

    /// <summary>
    /// States the handler for heartbeat packets (see <see cref="IHeartbeatHandler{TFrame, TPriority}"/>, with the packet type as its type argument): a heartbeat sent as a packet of its own, beneath
    /// packetization, so it is never split or reassembled and its receiver discards it. Optional, and when stated it is used instead of any heartbeat frame stated with
    /// <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.Heartbeat{THandler}"/>. Without either no heartbeats are sent. Heartbeats are never sent over HDLC.
    /// </summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel> Heartbeat<THandler>() where THandler : IHeartbeatHandler<TPacket, TPriority>;

    /// <summary>
    /// Sets the largest a serialized packet may be, in bytes,. Smaller packets let a higher-priority payload cut in
    /// sooner; larger ones carry less framing overhead. The default is 16 KiB. The engine measures what the serializer makes of a packet to see how much payload fits, so it must leave room for the packet's own fields.
    /// This is the only limit the engine applies: a packet or frame larger than a connection can carry (the HDLC <c>MaxInfoField</c>, say) fails to send, and the reason is logged.
    /// </summary>
    /// <param name="bytes">The largest serialized packet, in bytes.</param>
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel> Size(int bytes);

    /// <summary>
    /// Sets how many packets may be in flight over one connection at once. A higher-priority payload sent meanwhile goes out
    /// as soon as the packets in flight finish, so the window is how many it can end up waiting behind: 1 (the
    /// default) is the most responsive, while a wider window keeps a link with a long round trip busier. Must be at least 1.
    /// </summary>
    /// <param name="packets">The number of packets.</param>
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel> Window(int packets);
}
