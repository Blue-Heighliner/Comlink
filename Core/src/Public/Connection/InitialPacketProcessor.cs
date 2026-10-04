namespace BlueHeighliner.Comlink;

/// <summary>
/// Carries out an initial packet exchange on every new connection, so nodes can introduce themselves with packets, beneath the packetizer and before any initial message exchange: the host decides what to send, what
/// to make of what arrives, and when the connection counts as connected and as which user. Everything sent is a serialized instance of the packet type,
/// and nothing else crosses the connection. The engine has no notion of who starts the exchange or of requests and replies: <see cref="OnConnected"/> runs on both nodes, and
/// each packet a node receives before it is marked connected is given to <see cref="OnReceived"/>, so the processor decides from what it knows about the connection
/// (<see cref="IConnectionInfo"/>) who sends first and what answers what. Every node on a network must be configured alike. State one with <see cref="IPacketBuilder{TPacket}.InitialProcessor"/>.
/// </summary>
/// <typeparam name="TPacket">The host's packet type.</typeparam>
public interface IInitialPacketProcessor<TPacket> where TPacket : class
{
    /// <summary>Gets how long the exchange may take, from the connection forming, before the connection is dropped.</summary>
    TimeSpan Timeout { get; }

    /// <summary>Called on both nodes when the connection has formed, before anything has been received. A processor in which one node speaks first sends its initial packet here, on the node that decides that is it.</summary>
    /// <param name="context">Controls the connection.</param>
    void OnConnected(IInitialPacketContext<TPacket> context);

    /// <summary>Called for each packet received while the connection is not yet marked connected.</summary>
    /// <param name="context">Controls the connection.</param>
    /// <param name="packet">What arrived.</param>
    void OnReceived(IInitialPacketContext<TPacket> context, TPacket packet);
}
