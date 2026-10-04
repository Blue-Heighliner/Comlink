namespace BlueHeighliner.Comlink;

/// <summary>The engine's untyped view of the host's <see cref="IHeartbeatHandler{TFrame}"/>, working on frames as <see cref="object"/>.</summary>
internal interface IHeartbeatFrameHandler
{
    /// <summary>Gets the priority level heartbeats are sent with.</summary>
    Enum Priority { get; }
    /// <summary>Gets the time between heartbeats while they succeed.</summary>
    TimeSpan Interval { get; }
    /// <summary>Gets the time between heartbeats while they fail.</summary>
    TimeSpan RetryInterval { get; }
    /// <summary>Returns whether <paramref name="frame"/> is a heartbeat.</summary>
    bool IsValid(object frame);
    /// <summary>Creates a heartbeat frame.</summary>
    object Create();
}

/// <summary>Adapts a typed <see cref="IHeartbeatHandler{TFrame}"/> to <see cref="IHeartbeatFrameHandler"/>.</summary>
internal sealed class HeartbeatFrameHandler<TFrame>(IHeartbeatHandler<TFrame> handler) : IHeartbeatFrameHandler where TFrame : class
{
    /// <inheritdoc />
    public Enum Priority => handler.Priority;

    /// <inheritdoc />
    public TimeSpan Interval => handler.Interval;

    /// <inheritdoc />
    public TimeSpan RetryInterval => handler.RetryInterval;

    /// <inheritdoc />
    public bool IsValid(object frame) => handler.IsValid((TFrame)frame);

    /// <inheritdoc />
    public object Create() => handler.Create();
}
