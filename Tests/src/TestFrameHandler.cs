namespace BlueHeighliner.Comlink.Tests;

/// <summary>A frame handler that ignores every event, for configurations that need one but do not exercise it.</summary>
internal sealed class TestFrameHandler : IFrameHandler<TestFrame, TestMessagePriority, TestLevel, TestAspect>
{
    /// <inheritdoc />
    public Task OnConnected(INetworkConnectedContext<TestFrame, TestMessagePriority, TestLevel, TestAspect> context) => Task.CompletedTask;

    /// <inheritdoc />
    public Task OnDisconnected(INetworkDisconnectedContext<TestFrame, TestMessagePriority, TestLevel, TestAspect> context) => Task.CompletedTask;

    /// <inheritdoc />
    public Task OnReceived(INetworkReceivedContext<TestFrame, TestMessagePriority, TestLevel, TestAspect> context) => Task.CompletedTask;
}
