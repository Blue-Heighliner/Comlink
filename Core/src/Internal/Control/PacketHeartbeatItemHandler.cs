namespace BlueHeighliner.Comlink;

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
