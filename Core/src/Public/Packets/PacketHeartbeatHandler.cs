namespace BlueHeighliner.Comlink;

/// <summary>
/// Handles the packets of the host's packet type, <typeparamref name="TPacket"/>, that are heartbeats: sent by a node as a packet of its own, beneath packetization and so never split or reassembled, over each of its MSMT connections to check that the connection is really up and
/// to keep it live. A heartbeat is not a message, and the receiving node only acknowledges it. Heartbeats are optional: without a handler none are sent and a connection counts as up once it is
/// established. They are never sent over HDLC. State one with <see cref="IPacketBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Heartbeat{THandler}"/>; it takes precedence over a <see cref="IFrameHeartbeatHandler{TFrame, TPriority}"/>.
/// </summary>
/// <typeparam name="TPacket">The host's packet type.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
public interface IPacketHeartbeatHandler<TPacket, TPriority> where TPacket : class where TPriority : struct, Enum
{
    /// <summary>Gets the priority level that heartbeats are sent with, which is how they are ordered against other traffic.</summary>
    TPriority Priority { get; }

    /// <summary>Gets how long a connection waits between heartbeats while the last one succeeded.</summary>
    TimeSpan Interval { get; }

    /// <summary>Gets how long a connection waits before sending another heartbeat while the last one failed, which is how quickly a connection that is down is noticed to be back.</summary>
    TimeSpan RetryInterval { get; }

    /// <summary>Returns whether <paramref name="packet"/> is a heartbeat.</summary>
    /// <param name="packet">The packet to classify.</param>
    bool IsValid(TPacket packet);

    /// <summary>Creates a new heartbeat packet, for which <see cref="IsValid"/> returns <see langword="true"/>. The engine sets nothing else on it.</summary>
    /// <returns>The new packet.</returns>
    TPacket Create();
}
