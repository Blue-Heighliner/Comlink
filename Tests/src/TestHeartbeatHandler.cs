namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IHeartbeatHandler{TFrame}"/> for <see cref="TestFrame"/>, recognizing frames with <see cref="TestFrame.IsHeartbeat"/> set.</summary>
public sealed class TestHeartbeatHandler : IHeartbeatHandler<TestFrame>
{
    /// <inheritdoc />
    public Enum Priority { get; init; } = TestPriority.Normal;

    /// <inheritdoc />
    public bool IsValid(TestFrame frame) => frame.IsHeartbeat;

    /// <inheritdoc />
    public TestFrame Create() => new() { IsHidden = true, IsHeartbeat = true };
}
