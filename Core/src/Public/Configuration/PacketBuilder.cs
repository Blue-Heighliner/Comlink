namespace BlueHeighliner.Comlink;

/// <summary>
/// Describes the packets payloads are broken into for the network. The engine does all of the splitting and
/// reassembling itself; the host only says how a packet is stored and serialized, by mapping the five fields the engine
/// needs. Every field must be mapped. See <see cref="IEngineBuilder.Packets{TPacket}"/>.
/// </summary>
/// <typeparam name="TPacket">The host's packet type.</typeparam>
public interface IPacketBuilder<TPacket> where TPacket : class, new()
{
    /// <summary>Maps the identifier shared by every packet of one payload, which tells packets of different payloads apart.</summary>
    IPacketBuilder<TPacket> PayloadId(Func<TPacket, int> get, Action<TPacket, int> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.PayloadId</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IPacketBuilder<TPacket> PayloadId(Expression<Func<TPacket, int>> property);

    /// <summary>Maps the zero-based position of a packet among the packets of its payload.</summary>
    IPacketBuilder<TPacket> Index(Func<TPacket, int> get, Action<TPacket, int> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.Index</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IPacketBuilder<TPacket> Index(Expression<Func<TPacket, int>> property);

    /// <summary>Maps how many packets the payload was broken into.</summary>
    IPacketBuilder<TPacket> Count(Func<TPacket, int> get, Action<TPacket, int> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.Count</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IPacketBuilder<TPacket> Count(Expression<Func<TPacket, int>> property);

    /// <summary>Maps the length in bytes of the whole payload.</summary>
    IPacketBuilder<TPacket> PayloadLength(Func<TPacket, int> get, Action<TPacket, int> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.PayloadLength</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IPacketBuilder<TPacket> PayloadLength(Expression<Func<TPacket, int>> property);

    /// <summary>Maps the slice of the payload a packet carries. The value given to the setter is only valid for the duration of the call, so a packet that stores it must copy it.</summary>
    IPacketBuilder<TPacket> Data(Func<TPacket, ReadOnlyMemory<byte>> get, Action<TPacket, ReadOnlyMemory<byte>> set);

    /// <summary>
    /// Sets the largest a serialized packet may be, in bytes. Smaller packets let a higher-priority payload cut in
    /// sooner; larger ones carry less framing overhead. The default is 16 KiB. The engine measures what the serializer
    /// makes of a packet to see how much payload fits, so it must leave room for the packet's own fields.
    /// </summary>
    IPacketBuilder<TPacket> Size(int bytes);

    /// <summary>
    /// Sets how many packets may be in flight over one connection at once. A higher-priority payload sent meanwhile goes
    /// out as soon as the packets in flight finish, so the window is how many it can end up waiting behind: 1 (the
    /// default) is the most responsive, while a wider window keeps a link with a long round trip busier. Must be at least 1.
    /// </summary>
    IPacketBuilder<TPacket> Window(int packets);

    /// <summary>Replaces the serializer that turns packets into bytes. The default is a <see cref="ProtobufNetworkSerializer"/> that builds only <typeparamref name="TPacket"/>.</summary>
    IPacketBuilder<TPacket> Serializer(INetworkSerializer serializer);

    /// <summary>Replaces how a new, empty packet is created. The default is <c>new TPacket()</c>.</summary>
    IPacketBuilder<TPacket> Create(Func<TPacket> create);

    /// <summary>
    /// States how nodes introduce themselves on a new connection, with packets: the processor is told when a connection forms and given each packet that
    /// arrives until it marks the connection connected as a named user (see <see cref="IInitialPacketProcessor{TPacket}"/>). What it sends is a serialized
    /// instance of the packet type sent as it is, beneath the packetizer and before any initial message exchange. Every node on a network must be configured alike.
    /// Like every other thing sent between nodes it is a serialized instance of the message or packet type and nothing else.
    /// </summary>
    /// <typeparam name="TProcessor">The processor type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IPacketBuilder<TPacket> InitialProcessor<TProcessor>() where TProcessor : IInitialPacketProcessor<TPacket>;
}
