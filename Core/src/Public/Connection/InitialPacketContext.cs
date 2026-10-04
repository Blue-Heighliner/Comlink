namespace BlueHeighliner.Comlink;

/// <summary>
/// Handed to an <see cref="IInitialPacketProcessor{TPacket}"/> for one connection that has just formed, to carry out the initial packet exchange on it:
/// send packets, then either mark the connection fully connected as a named user or disconnect it. Until one of those happens the connection is unusable,
/// and it is dropped if it takes too long.
/// </summary>
/// <typeparam name="TPacket">The host's packet type.</typeparam>
public interface IInitialPacketContext<TPacket> : IEngineContext where TPacket : class
{
    /// <summary>Gets what is known about the connection.</summary>
    IConnectionInfo Connection { get; }

    /// <summary>Marks the connection fully connected, as <paramref name="userName"/>. The connection is then usable, and anything after it is ordinary traffic.</summary>
    /// <param name="userName">The user on the other end.</param>
    void Connected(string userName);

    /// <summary>Drops the connection.</summary>
    void Disconnect();

    /// <summary>Sends <paramref name="packet"/> over the connection in the background, in order with this context's other calls (a <see cref="Connected"/> after a send takes effect once it has been sent), and dropping the connection if it cannot be sent,, serialized with the packet serializer and sent as it is, since it is itself a packet and is not split.</summary>
    /// <param name="packet">What to send.</param>
    void Send(TPacket packet);
}
