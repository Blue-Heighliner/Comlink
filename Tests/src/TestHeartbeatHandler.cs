namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IHeartbeatHandler{TFrame}"/> for <see cref="TestFrame"/>, recognizing frames with <see cref="TestFrame.IsHeartbeat"/> set.</summary>
public sealed class TestHeartbeatHandler : IHeartbeatHandler<TestFrame>
{
    /// <inheritdoc />
    public Enum Priority { get; init; } = TestPriority.Normal;

    /// <inheritdoc />
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public TimeSpan RetryInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public bool IsValid(TestFrame frame) => frame.IsHeartbeat;

    /// <inheritdoc />
    public TestFrame Create() => new() { IsHidden = true, IsHeartbeat = true };
}
