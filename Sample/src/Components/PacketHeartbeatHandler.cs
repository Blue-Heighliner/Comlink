namespace BlueHeighliner.Comlink.Sample;

/// <summary>Treats a <see cref="Packet"/> as a heartbeat when its <see cref="Packet.IsHeartbeat"/> flag is set, sending heartbeats at the lowest user priority as packets of their own.</summary>
public sealed class PacketHeartbeatHandler : IHeartbeatHandler<Packet>
{
    /// <inheritdoc />
    public Enum Priority => MessagePriority.Low;

    /// <inheritdoc />
    public bool IsValid(Packet packet) => packet.IsHeartbeat;

    /// <inheritdoc />
    public Packet Create() => new() { IsHeartbeat = true };
}
