namespace BlueHeighliner.Comlink;

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
