namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="NetworkProcessing"/>.</summary>
public sealed class NetworkProcessingTests
{
    private static Message MakeMessage() => new() { Id = "M1", FromUser = "ALICE", Body = "Hi", Addresses = [], SentAt = DateTime.UtcNow, Priority = TestMessagePriority.Normal };

    private static NetworkProcessing Build(Mock<IEngineFrameHandler>? handler, Mock<INetworkEnvironment> environment, TrackingFrameSerializer? frames = null)
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.FrameHandler).Returns(handler?.Object);
        if (frames is not null)
        {
            controller.Setup(c => c.FrameSerializer).Returns(frames);
        }

        return new NetworkProcessing(controller.Object, environment.Object, LoggerFactory.Create(_ => { }));
    }

    /// <summary>Each event reaches the handler with the environment and its arguments.</summary>
    [Fact]
    public async Task Events_ReachTheHandler()
    {
        Mock<INetworkEnvironment> environment = new();
        Mock<IEngineFrameHandler> handler = new();
        TaskCompletionSource done = new();
        handler.Setup(h => h.OnSent(environment.Object, It.IsAny<Message>())).Returns(() => { done.SetResult(); return Task.CompletedTask; });
        NetworkProcessing processing = Build(handler, environment);

        processing.Sent(MakeMessage());

        await done.Task.WaitAsync(TimeSpan.FromSeconds(10));
        handler.Verify(h => h.OnSent(environment.Object, It.Is<Message>(m => m.Id == "M1")), Times.Once);
    }

    /// <summary>A handler that throws is contained and does not escape to the caller.</summary>
    [Fact]
    public async Task Events_AHandlerThatThrows_IsContained()
    {
        Mock<INetworkEnvironment> environment = new();
        Mock<IEngineFrameHandler> handler = new();
        TaskCompletionSource done = new();
        handler.Setup(h => h.OnConnected(environment.Object, "BOB")).Returns(() => { done.SetResult(); throw new InvalidOperationException(); });
        NetworkProcessing processing = Build(handler, environment);

        processing.Connected("BOB");

        await done.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    /// <summary>Without a handler there is nobody to take a retrieval, which reports false, and other events are ignored.</summary>
    [Fact]
    public void WithoutAHandler_RetrievalIsRefused_AndEventsAreIgnored()
    {
        NetworkProcessing processing = Build(null, new Mock<INetworkEnvironment>());

        processing.Disconnected("BOB");

        Assert.False(processing.Retrieval("SERVER", new RetrievalCriteria()));
    }

    /// <summary>With a handler a retrieval is accepted and passed on.</summary>
    [Fact]
    public async Task Retrieval_IsPassedToTheHandler()
    {
        Mock<INetworkEnvironment> environment = new();
        Mock<IEngineFrameHandler> handler = new();
        TaskCompletionSource done = new();
        RetrievalCriteria criteria = new();
        handler.Setup(h => h.OnRetrieval(environment.Object, "SERVER", criteria)).Returns(() => { done.SetResult(); return Task.CompletedTask; });
        NetworkProcessing processing = Build(handler, environment);

        Assert.True(processing.Retrieval("SERVER", criteria));

        await done.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    /// <summary>A frame the handler is given is the handler's to dispose, so the engine disposes it only when there is no handler to give it to, and never a frame from an external system.</summary>
    [Fact]
    public async Task Received_DisposesTheFrameOnlyWhenNoHandlerGetsIt()
    {
        Mock<INetworkEnvironment> environment = new();
        Mock<IEngineFrameHandler> handler = new();
        TaskCompletionSource handled = new();
        handler.Setup(h => h.OnReceived(environment.Object, It.IsAny<object>(), FrameOrigin.Peer, "BOB")).Returns(() =>
        {
            handled.SetResult();
            return Task.CompletedTask;
        });
        TestFrame given = new();
        Build(handler, environment).Received(given, FrameOrigin.Peer, "BOB");
        await handled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(50);

        TestFrame unhandled = new();
        TestFrame external = new();
        NetworkProcessing none = Build(null, environment);
        none.Received(unhandled, FrameOrigin.Peer, "BOB");
        none.Received(external, FrameOrigin.ExternalSystem, "FEED");

        Assert.False(given.IsDisposed);
        Assert.True(unhandled.IsDisposed);
        Assert.False(external.IsDisposed);
    }
}
