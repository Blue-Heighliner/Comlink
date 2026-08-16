namespace BlueHeighliner.Comlink.Tests.ExternalSystems;

/// <summary>Unit tests for <see cref="ExternalSystemsService"/>'s relay and normal-processing coordination.</summary>
public sealed class ExternalSystemsServiceTests
{
    private sealed class FakePeerService : IPeerService
    {
        public event Func<object, Task>? MessageDelivered;
#pragma warning disable CS0067
        public event Func<string, string, Task>? ConfirmationReceived;
        public event Func<string, string, OftDeliveryStatus, Task>? DeliveryStatusChanged;
#pragma warning restore CS0067
        public Task Start(CancellationToken cancellation) => Task.CompletedTask;
        public Task<bool> Send(string userName, object message, CancellationToken cancellation = default) => Task.FromResult(true);

        public List<object> DeliveredLocally { get; } = [];

        public async Task DeliverLocal(object payload)
        {
            DeliveredLocally.Add(payload);
            if (MessageDelivered is not null) { await MessageDelivered(payload); }
        }

        public async Task FireMessageDelivered(object payload)
        {
            if (MessageDelivered is not null) { await MessageDelivered(payload); }
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
        controller.Setup(c => c.GetExternalSystems()).Returns(systems);
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

        await service.Start(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
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
        await startTask.WaitAsync(TimeSpan.FromSeconds(2));
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

        TestMessage message = new() { MessageId = "M1" };
        await peer.FireMessageDelivered(message);

        Assert.Single(systemA.SentMessages);
        Assert.Same(message, systemA.SentMessages[0]);
        Assert.Single(systemB.SentMessages);
        Assert.Same(message, systemB.SentMessages[0]);

        cts.Cancel();
        await startTask.WaitAsync(TimeSpan.FromSeconds(2));
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

        TestMessage message = new() { MessageId = "M1" };
        await systemA.Deliver(message);

        Assert.Single(peer.DeliveredLocally);
        Assert.Same(message, peer.DeliveredLocally[0]);

        cts.Cancel();
        await startTask.WaitAsync(TimeSpan.FromSeconds(2));
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

        TestMessage message = new() { MessageId = "M1" };
        await systemA.Deliver(message);

        Assert.Empty(systemA.SentMessages);
        Assert.Single(systemB.SentMessages);
        Assert.Same(message, systemB.SentMessages[0]);
        Assert.Single(systemC.SentMessages);
        Assert.Same(message, systemC.SentMessages[0]);

        cts.Cancel();
        await startTask.WaitAsync(TimeSpan.FromSeconds(2));
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

        TestMessage messageFromA = new() { MessageId = "FromA" };
        TestMessage messageFromB = new() { MessageId = "FromB" };
        await Task.WhenAll(systemA.Deliver(messageFromA), systemB.Deliver(messageFromB));

        await WaitUntil(() => systemA.SentMessages.Count >= 1 && systemB.SentMessages.Count >= 1, TimeSpan.FromSeconds(2));

        Assert.DoesNotContain(messageFromA, systemA.SentMessages);
        Assert.Contains(messageFromA, systemB.SentMessages);
        Assert.DoesNotContain(messageFromB, systemB.SentMessages);
        Assert.Contains(messageFromB, systemA.SentMessages);

        cts.Cancel();
        await startTask.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
