namespace BlueHeighliner.Comlink;

/// <summary>
/// Carries out an initial packet exchange on every new connection, so nodes can introduce themselves with packets, beneath the packetizer and before any initial message exchange: the host decides what to send, what
/// to make of what arrives, and when the connection counts as connected and as which user. Everything sent is a serialized instance of the packet type,
/// and nothing else crosses the connection, so what the other node sends is recognized by position: once a connection has formed, each packet a node
/// receives before it is marked connected is given to <see cref="OnInitial"/> on the accepting node or <see cref="OnReply"/> on the opening node. Every node on a
/// network must be configured alike. State one with <see cref="IPacketBuilder{TPacket}.InitialProcessor"/>.
/// </summary>
/// <typeparam name="TPacket">The host's packet type.</typeparam>
public interface IInitialPacketProcessor<TPacket> where TPacket : class
{
    /// <summary>Called on both nodes when the connection has formed, before anything has been received. The opening node (<see cref="IInitialPacketContext{TPacket}.IsOpener"/>) usually sends its initial packet here.</summary>
    /// <param name="context">Controls the connection.</param>
    void OnConnected(IInitialPacketContext<TPacket> context);

    /// <summary>Called on the accepting node for each packet the opening node sent, while the connection is not yet marked connected.</summary>
    /// <param name="context">Controls the connection.</param>
    /// <param name="packet">What arrived.</param>
    void OnInitial(IInitialPacketContext<TPacket> context, TPacket packet);

    /// <summary>Called on the opening node for each packet the accepting node sent, while the connection is not yet marked connected.</summary>
    /// <param name="context">Controls the connection.</param>
    /// <param name="packet">What arrived.</param>
    void OnReply(IInitialPacketContext<TPacket> context, TPacket packet);
}
