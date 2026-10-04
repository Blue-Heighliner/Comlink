namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IHeartbeatHandler{TFrame}"/> for <see cref="TestPacket"/>, recognizing packets with <see cref="TestPacket.IsHeartbeat"/> set.</summary>
public sealed class TestPacketHeartbeatHandler : IHeartbeatHandler<TestPacket>
{
    /// <inheritdoc />
    public Enum Priority { get; init; } = TestPriority.Normal;

    /// <inheritdoc />
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public TimeSpan RetryInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public bool IsValid(TestPacket packet) => packet.IsHeartbeat;

    /// <inheritdoc />
    public TestPacket Create() => new() { IsHeartbeat = true };
}
