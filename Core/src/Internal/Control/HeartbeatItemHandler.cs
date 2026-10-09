namespace BlueHeighliner.Comlink;

/// <summary>The engine's untyped view of the host's <see cref="IFrameHeartbeatHandler{TFrame, TPriority}"/> or <see cref="IPacketHeartbeatHandler{TPacket, TPriority}"/>, working on the frame or packet as <see cref="object"/>.</summary>
internal interface IHeartbeatItemHandler
{
    /// <summary>Gets the priority level heartbeats are sent with.</summary>
    Enum Priority { get; }
    /// <summary>Gets the time between heartbeats while they succeed.</summary>
    TimeSpan Interval { get; }
    /// <summary>Gets the time between heartbeats while they fail.</summary>
    TimeSpan RetryInterval { get; }
    /// <summary>Returns whether <paramref name="item"/> is a heartbeat.</summary>
    bool IsValid(object item);
    /// <summary>Creates a heartbeat.</summary>
    object Create();
}
