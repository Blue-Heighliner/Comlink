namespace BlueHeighliner.Comlink.Tests.Unit.Internal.ExternalSystems;

/// <summary>Unit tests for <see cref="ExternalSystemsService"/>'s relay and normal-processing coordination.</summary>
public sealed class ExternalSystemsServiceTests
{
    private sealed class FakePeerService : IPeerService
    {
        public event Func<object, Task>? FrameDelivered;
#pragma warning disable CS0067
        public event Func<string, string, Task>? ReadReceiptReceived;
        public event Func<string, string, Task>? ReceiveReceiptReceived;
        public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;
        public event Func<string, Task>? UserConnected;
        public event Func<string, Task>? UserDisconnected;
#pragma warning restore CS0067
        public IReadOnlyList<string> GetConnectedUsers() => [];
        public bool IsUserConnected(string userName) => false;
        public Task Start(CancellationToken cancellation) => Task.CompletedTask;
        public Task<bool> Send(string userName, object message, CancellationToken cancellation = default) => Task.FromResult(true);
        public Task<bool> SendPacket(string userName, object packet, CancellationToken cancellation = default) => Task.FromResult(true);

        public List<object> DeliveredLocally { get; } = [];

        public async Task DeliverLocal(object payload)
        {
            DeliveredLocally.Add(payload);
            if (FrameDelivered is not null) { await FrameDelivered(payload); }
        }

        public async Task FireMessageDelivered(object payload)
        {
            if (FrameDelivered is not null) { await FrameDelivered(payload); }
        }
    }

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
            if (MessageReceived is not null) { await MessageReceived(message); }
        }
    }

    private static readonly ILoggerFactory noLogger = LoggerFactory.Create(_ => { });

    private static IEngineController MakeController(IReadOnlyList<IExternalSystem> systems)
    {
        Mock<IEngineController> controller = new();
        controller.Setup(c => c.IsMessage(It.IsAny<object>())).Returns(true);
        controller.Setup(c => c.GetMessageId(It.IsAny<object>())).Returns((object message) => ((TestFrame)message).MessageId);
        controller.Setup(c => c.ExternalSystems).Returns(systems);
        return controller.Object;
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) { throw new TimeoutException("Condition was not met in time."); }
            await Task.Delay(10);
        }
    }

    /// <summary>With no configured external systems, Start returns immediately without subscribing to anything.</summary>
    [Fact]
    public async Task Start_NoExternalSystems_ReturnsImmediately()
    {
        FakePeerService peer = new();
        ExternalSystemsService service = new(MakeController([]), peer, noLogger);

        await service.Start(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>A message an external system delivers without an identifier is invalid and is dropped.</summary>
    [Fact]
    public async Task ExternalSystemMessageReceived_WithoutAnId_IsDropped()
    {
        FakeExternalSystem systemA = new("A");
        FakePeerService peer = new();
        ExternalSystemsService service = new(MakeController([systemA]), peer, noLogger);
        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(50);

        await systemA.Deliver(new TestFrame());

        Assert.Empty(peer.DeliveredLocally);
        cts.Cancel();
        await startTask.WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>Start runs every configured external system's own Start loop concurrently.</summary>
    [Fact]
    public async Task Start_RunsEachExternalSystemsStartLoop()
    {
        FakeExternalSystem systemA = new("A");
        FakeExternalSystem systemB = new("B");
        FakePeerService peer = new();
        ExternalSystemsService service = new(MakeController([systemA, systemB]), peer, noLogger);

        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);

        await Task.Delay(50);
        Assert.False(startTask.IsCompleted);

        cts.Cancel();
        await startTask.WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>A message the app receives (via peer delivery) is relayed out through every external system.</summary>
    [Fact]
    public async Task PeerMessageDelivered_RelaysToEveryExternalSystem()
    {
        FakeExternalSystem systemA = new("A");
        FakeExternalSystem systemB = new("B");
        FakePeerService peer = new();
        ExternalSystemsService service = new(MakeController([systemA, systemB]), peer, noLogger);

        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(50);

        TestFrame message = new() { MessageId = "M1" };
        await peer.FireMessageDelivered(message);

        Assert.Single(systemA.SentMessages);
        Assert.Same(message, systemA.SentMessages[0]);
        Assert.Single(systemB.SentMessages);
        Assert.Same(message, systemB.SentMessages[0]);

        cts.Cancel();
        await startTask.WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>A message received from an external system is processed as an ordinary received message (delivered locally).</summary>
    [Fact]
    public async Task ExternalSystemMessageReceived_ProcessesAsOrdinaryReceivedMessage()
    {
        FakeExternalSystem systemA = new("A");
        FakePeerService peer = new();
        ExternalSystemsService service = new(MakeController([systemA]), peer, noLogger);

        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(50);

        TestFrame message = new() { MessageId = "M1" };
        await systemA.Deliver(message);

        Assert.Single(peer.DeliveredLocally);
        Assert.Same(message, peer.DeliveredLocally[0]);

        cts.Cancel();
        await startTask.WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>A message received from one external system is relayed to every other external system, but not back to its own source.</summary>
    [Fact]
    public async Task ExternalSystemMessageReceived_RelaysToOtherSystemsExceptSource()
    {
        FakeExternalSystem systemA = new("A");
        FakeExternalSystem systemB = new("B");
        FakeExternalSystem systemC = new("C");
        FakePeerService peer = new();
        ExternalSystemsService service = new(MakeController([systemA, systemB, systemC]), peer, noLogger);

        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(50);

        TestFrame message = new() { MessageId = "M1" };
        await systemA.Deliver(message);

        Assert.Empty(systemA.SentMessages);
        Assert.Single(systemB.SentMessages);
        Assert.Same(message, systemB.SentMessages[0]);
        Assert.Single(systemC.SentMessages);
        Assert.Same(message, systemC.SentMessages[0]);

        cts.Cancel();
        await startTask.WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>Concurrent deliveries from two different external systems each exclude only their own source, not each other's.</summary>
    [Fact]
    public async Task ExternalSystemMessageReceived_ConcurrentDeliveries_EachExcludesOnlyItsOwnSource()
    {
        FakeExternalSystem systemA = new("A");
        FakeExternalSystem systemB = new("B");
        FakePeerService peer = new();
        ExternalSystemsService service = new(MakeController([systemA, systemB]), peer, noLogger);

        using CancellationTokenSource cts = new();
        Task startTask = service.Start(cts.Token);
        await Task.Delay(50);

        TestFrame messageFromA = new() { MessageId = "FromA" };
        TestFrame messageFromB = new() { MessageId = "FromB" };
        await Task.WhenAll(systemA.Deliver(messageFromA), systemB.Deliver(messageFromB));

        await WaitUntil(() => systemA.SentMessages.Count >= 1 && systemB.SentMessages.Count >= 1, TimeSpan.FromSeconds(30));

        Assert.DoesNotContain(messageFromA, systemA.SentMessages);
        Assert.Contains(messageFromA, systemB.SentMessages);
        Assert.DoesNotContain(messageFromB, systemB.SentMessages);
        Assert.Contains(messageFromB, systemA.SentMessages);

        cts.Cancel();
        await startTask.WaitAsync(TimeSpan.FromSeconds(30));
    }

}
