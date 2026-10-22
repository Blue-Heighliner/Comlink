namespace BlueHeighliner.Comlink;

/// <summary>
/// Handed to an <see cref="IPacketHandshakeHandler{TPacket}"/> for one connection that has just formed, to carry out the handshake on it:
/// send packets, then either mark the connection fully connected as a named user or disconnect it. Until one of those happens the connection is unusable,
/// and it is dropped if it takes too long.
/// </summary>
/// <typeparam name="TPacket">The host's packet type.</typeparam>
public interface IPacketHandshakeContext<TPacket> where TPacket : class
{
    /// <summary>Gets what is known about the connection.</summary>
    IConnectionInfo Connection { get; }

    /// <summary>Accepts the other node: marks the connection fully connected, as <paramref name="userName"/>. The connection is then usable, and anything after it is ordinary traffic.</summary>
    /// <param name="userName">The user on the other end.</param>
    Task Accept(string userName);

    /// <summary>Rejects the other node: drops the connection, which fails whatever was connecting, and logs <paramref name="reason"/> as why.</summary>
    /// <param name="reason">Why the connection is refused, in words for the log, such as <c>The other node did not know the password</c>.</param>
    Task Reject(string reason);

    /// <summary>Sends <paramref name="packet"/> over the connection and completes once the other node has accepted it, so a handler awaits it before calling <see cref="Accept"/>. The connection is dropped if it cannot be sent, serialized with the packet serializer and sent as it is, since it is itself a packet and is not split.</summary>
    /// <param name="packet">What to send.</param>
    /// <returns>Whether the other node accepted it.</returns>
    Task<bool> Send(TPacket packet);
}
