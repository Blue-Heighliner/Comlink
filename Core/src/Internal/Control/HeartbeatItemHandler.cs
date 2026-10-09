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

/// <summary>Adapts a host's <see cref="IFrameHeartbeatHandler{TFrame, TPriority}"/> to <see cref="IHeartbeatItemHandler"/>.</summary>
internal sealed class FrameHeartbeatItemHandler<TFrame, TPriority>(IFrameHeartbeatHandler<TFrame, TPriority> handler) : IHeartbeatItemHandler where TFrame : class where TPriority : struct, Enum
{
    /// <inheritdoc />
    public Enum Priority => handler.Priority;

    /// <inheritdoc />
    public TimeSpan Interval => handler.Interval;

    /// <inheritdoc />
    public TimeSpan RetryInterval => handler.RetryInterval;

    /// <inheritdoc />
    public bool IsValid(object item) => handler.IsValid((TFrame)item);

    /// <inheritdoc />
    public object Create() => handler.Create();
}

/// <summary>Adapts a host's <see cref="IPacketHeartbeatHandler{TPacket, TPriority}"/> to <see cref="IHeartbeatItemHandler"/>.</summary>
internal sealed class PacketHeartbeatItemHandler<TPacket, TPriority>(IPacketHeartbeatHandler<TPacket, TPriority> handler) : IHeartbeatItemHandler where TPacket : class where TPriority : struct, Enum
{
    /// <inheritdoc />
    public Enum Priority => handler.Priority;

    /// <inheritdoc />
    public TimeSpan Interval => handler.Interval;

    /// <inheritdoc />
    public TimeSpan RetryInterval => handler.RetryInterval;

    /// <inheritdoc />
    public bool IsValid(object item) => handler.IsValid((TPacket)item);

    /// <inheritdoc />
    public object Create() => handler.Create();
}
