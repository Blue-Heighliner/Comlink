namespace BlueHeighliner.Comlink;

/// <summary>
/// Handles the frames of the host's frame type, or the packets of its packet type, <typeparamref name="TFrame"/> that are heartbeats: sent by a node over each of its MSMT connections to check that the connection is really up and
/// to keep it live. A heartbeat is not a message, and the receiving node only acknowledges it. Heartbeats are optional: without a handler none are sent and a connection counts as up once it is
/// established. They are never sent over HDLC. See <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.Heartbeat{THandler}"/> and <see cref="IPacketBuilder{TFrame, TPacket, TPriority, TLevel}.Heartbeat{THandler}"/>, the latter of which sends the heartbeat as a packet of its own, beneath packetization, and takes precedence.
/// </summary>
/// <typeparam name="TFrame">The host's frame type, or its packet type for a packet heartbeat.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
public interface IHeartbeatHandler<TFrame, TPriority> where TFrame : class where TPriority : struct, Enum
{
    /// <summary>Gets the priority level that heartbeats are sent with, which is how they are ordered against other traffic.</summary>
    TPriority Priority { get; }

    /// <summary>Gets how long a connection waits between heartbeats while the last one succeeded.</summary>
    TimeSpan Interval { get; }

    /// <summary>Gets how long a connection waits before sending another heartbeat while the last one failed, which is how quickly a connection that is down is noticed to be back.</summary>
    TimeSpan RetryInterval { get; }

    /// <summary>Returns whether <paramref name="frame"/> is a heartbeat.</summary>
    /// <param name="frame">The frame to classify.</param>
    bool IsValid(TFrame frame);

    /// <summary>Creates a new heartbeat frame, for which <see cref="IsValid"/> returns <see langword="true"/>. The engine sets nothing else on it.</summary>
    /// <returns>The new frame.</returns>
    TFrame Create();
}
