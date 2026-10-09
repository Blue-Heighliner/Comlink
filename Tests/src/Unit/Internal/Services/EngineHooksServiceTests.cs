namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="EngineHooksService"/>, which hands what the peer service reports to the frame handler.</summary>
public sealed class EngineHooksServiceTests
{
    private static (EngineHooksService Service, FakePeerService Peer, Mock<INetworkProcessing> Processing, Mock<IEngineController> Controller) Build(bool handler = true)
    {
        FakePeerService peer = new();
        Mock<INetworkProcessing> processing = new();
        Mock<IEngineController> controller = new();
        controller.Setup(c => c.FrameHandler).Returns(handler ? Mock.Of<IEngineFrameHandler>() : null);
        return (new EngineHooksService(peer, controller.Object, processing.Object), peer, processing, controller);
    }

    /// <summary>With no handler configured, Start returns immediately without subscribing to any peer event.</summary>
    [Fact]
    public async Task Start_NoHandlerConfigured_ReturnsImmediatelyWithoutSubscribing()
    {
        (EngineHooksService service, FakePeerService peer, _, _) = Build(handler: false);

        await service.Start(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.False(peer.HasSubscribers);
        Assert.False(peer.HasFrameSubscribers);
    }

    /// <summary>With a handler, Start subscribes to every peer event and blocks until cancelled, then unsubscribes.</summary>
    [Fact]
    public async Task Start_WithAHandler_SubscribesAndBlocksUntilCancelled()
    {
        (EngineHooksService service, FakePeerService peer, _, _) = Build();
        using CancellationTokenSource cts = new();

        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);

        Assert.True(peer.HasSubscribers);
        Assert.True(peer.HasFrameSubscribers);
        Assert.False(startTask.IsCompleted);

        cts.Cancel();
        await startTask.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(peer.HasSubscribers);
        Assert.False(peer.HasFrameSubscribers);
    }

    /// <summary>A user connecting, a user disconnecting and a frame arriving each reach the network processing, the frame with the user it arrived from and as coming from a peer.</summary>
    [Fact]
    public async Task PeerEvents_ReachTheNetworkProcessing()
    {
        (EngineHooksService service, FakePeerService peer, Mock<INetworkProcessing> processing, _) = Build();
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(20);
        TestFrame frame = new() { MessageId = "M1" };

        await peer.Connect("BOB");
        await peer.Disconnect("BOB");
        await peer.Receive(frame, "SERVER");

        processing.Verify(p => p.Connected("BOB"), Times.Once);
        processing.Verify(p => p.Disconnected("BOB"), Times.Once);
        processing.Verify(p => p.Received(frame, FrameOrigin.Peer, "SERVER"), Times.Once);
        cts.Cancel();
        await startTask.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
