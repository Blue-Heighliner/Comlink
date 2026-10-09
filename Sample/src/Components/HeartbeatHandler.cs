namespace BlueHeighliner.Comlink.Sample;

/// <summary>Treats a <see cref="Packet"/> as a heartbeat when its <see cref="Packet.IsHeartbeat"/> flag is set, sending heartbeats at the lowest user priority as packets of their own.</summary>
public sealed class HeartbeatHandler : IPacketHeartbeatHandler<Packet, Priority>
{
    /// <inheritdoc />
    public Priority Priority => Priority.Low;

    /// <inheritdoc />
    public TimeSpan Interval { get; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public TimeSpan RetryInterval { get; } = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public bool IsValid(Packet packet) => packet.IsHeartbeat;

    /// <inheritdoc />
    public Packet Create() => new() { IsHeartbeat = true };
}
