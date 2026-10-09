namespace BlueHeighliner.Comlink.Tests.Unit.Internal.ExternalSystems;

/// <summary>Unit tests for <see cref="ExternalSystemsService"/>: running the systems, handing what they deliver to the frame handler, and sending to them.</summary>
public sealed class ExternalSystemsServiceTests
{
    private sealed class FakeExternalSystem(string name) : IExternalSystem
    {
        public event Func<object, Task>? MessageReceived;
        public string Name { get; } = name;
        public bool IsConnected { get; set; } = true;
        public List<object> SentMessages { get; } = [];

        public async Task Start(CancellationToken cancellation)
        {
            try { await Task.Delay(Timeout.Infinite, cancellation); }
            catch (OperationCanceledException) { }
        }

        public Task<bool> Send(object message)
        {
            SentMessages.Add(message);
            return Task.FromResult(true);
        }

        public void AttachLogger(ILogger logger) { }

        public async Task Deliver(object message)
        {
            if (MessageReceived is not null)
            {
                await MessageReceived(message);
            }
        }
    }

    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });

    private static IEngineController MakeController(IReadOnlyList<IExternalSystem> systems)
    {
        Mock<IEngineController> controller = new();
        controller.Setup(c => c.FrameType).Returns(typeof(TestFrame));
        controller.Setup(c => c.ExternalSystems).Returns(systems);
        return controller.Object;
    }

    /// <summary>With no configured external systems, Start returns immediately.</summary>
    [Fact]
    public async Task Start_NoExternalSystems_ReturnsImmediately()
    {
        ExternalSystemsService service = new(MakeController([]), Mock.Of<INetworkProcessing>(), noLogger);

        await service.Start(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>Start runs every configured external system's own Start loop concurrently, until cancelled.</summary>
    [Fact]
    public async Task Start_RunsEachExternalSystemsStartLoop()
    {
        ExternalSystemsService service = new(MakeController([new FakeExternalSystem("A"), new FakeExternalSystem("B")]), Mock.Of<INetworkProcessing>(), noLogger);

        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);

        await Task.Delay(50);
        Assert.False(startTask.IsCompleted);

        cts.Cancel();
        await startTask.WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>A frame an external system delivers is handed to the frame handler as received from that system, and the engine does nothing else with it.</summary>
    [Fact]
    public async Task ExternalSystemFrame_IsHandedToTheHandler()
    {
        FakeExternalSystem system = new("A");
        Mock<INetworkProcessing> processing = new();
        ExternalSystemsService service = new(MakeController([system]), processing.Object, noLogger);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(50);

        TestFrame frame = new() { MessageId = "M1" };
        await system.Deliver(frame);

        processing.Verify(p => p.Received(frame, FrameOrigin.ExternalSystem, "A"), Times.Once);
        cts.Cancel();
        await startTask.WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>Something that is not an instance of the configured frame type is not handed to the handler.</summary>
    [Fact]
    public async Task ExternalSystemObjectOfAnotherType_IsIgnored()
    {
        FakeExternalSystem system = new("A");
        Mock<INetworkProcessing> processing = new();
        ExternalSystemsService service = new(MakeController([system]), processing.Object, noLogger);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(50);

        await system.Deliver("not a frame");

        processing.Verify(p => p.Received(It.IsAny<object>(), It.IsAny<FrameOrigin>(), It.IsAny<string>()), Times.Never);
        cts.Cancel();
        await startTask.WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>Send hands a frame to every external system.</summary>
    [Fact]
    public async Task Send_GoesToEveryExternalSystem()
    {
        FakeExternalSystem systemA = new("A");
        FakeExternalSystem systemB = new("B");
        ExternalSystemsService service = new(MakeController([systemA, systemB]), Mock.Of<INetworkProcessing>(), noLogger);
        TestFrame frame = new() { MessageId = "M1" };

        await service.Send(frame);

        Assert.Same(frame, Assert.Single(systemA.SentMessages));
        Assert.Same(frame, Assert.Single(systemB.SentMessages));
    }
}
