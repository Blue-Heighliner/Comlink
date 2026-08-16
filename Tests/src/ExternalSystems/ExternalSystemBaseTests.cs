namespace BlueHeighliner.Comlink.Tests.ExternalSystems;

/// <summary>Unit tests for <see cref="ExternalSystemBase{TMessage}"/>'s connect/poll/disconnect/send/receive lifecycle.</summary>
public sealed class ExternalSystemBaseTests
{
    private sealed class FakeExternalSystem(TimeSpan connectRetryInterval, TimeSpan pollInterval)
        : ExternalSystemBase<TestMessage>("Fake", connectRetryInterval, pollInterval)
    {
        public Func<CancellationToken, Task<bool>> TryConnectImpl { get; set; } = _ => Task.FromResult(true);
        public Func<CancellationToken, Task<bool>> PollIsConnectedImpl { get; set; } = _ => Task.FromResult(true);
        public Func<TestMessage, Task<bool>> SendImpl { get; set; } = _ => Task.FromResult(true);
        public int DisconnectCallCount { get; private set; }
        public List<TestMessage> SentMessages { get; } = [];

        protected override Task<bool> TryConnect(CancellationToken cancellation) => TryConnectImpl(cancellation);
        protected override Task<bool> PollIsConnected(CancellationToken cancellation) => PollIsConnectedImpl(cancellation);

        protected override Task Disconnect()
        {
            DisconnectCallCount++;
            return Task.CompletedTask;
        }

        protected override Task<bool> Send(TestMessage message)
        {
            SentMessages.Add(message);
            return SendImpl(message);
        }

        public Task Deliver(TestMessage message) => Receive(message);
    }

    private sealed class NonPollingFakeExternalSystem(TimeSpan connectRetryInterval, TimeSpan pollInterval)
        : ExternalSystemBase<TestMessage>("NonPolling", connectRetryInterval, pollInterval)
    {
        public int DisconnectCallCount { get; private set; }

        protected override Task<bool> TryConnect(CancellationToken cancellation) => Task.FromResult(true);

        protected override Task Disconnect()
        {
            DisconnectCallCount++;
            return Task.CompletedTask;
        }

        protected override Task<bool> Send(TestMessage message) => Task.FromResult(true);

        public void SimulateDisconnect() => ReportDisconnected();
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

    /// <summary>Name returns the constructor-provided value.</summary>
    [Fact]
    public void Name_ReturnsConstructorValue()
    {
        FakeExternalSystem system = new(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        Assert.Equal("Fake", system.Name);
    }

    /// <summary>Before Start ever connects, IsConnected is false.</summary>
    [Fact]
    public void IsConnected_Initially_False()
    {
        FakeExternalSystem system = new(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        Assert.False(system.IsConnected);
    }

    /// <summary>Start transitions IsConnected to true once TryConnect succeeds.</summary>
    [Fact]
    public async Task Start_TryConnectSucceeds_BecomesConnected()
    {
        FakeExternalSystem system = new(TimeSpan.FromMilliseconds(20), TimeSpan.FromSeconds(30));
        using CancellationTokenSource cts = new();
        Task startTask = system.Start(cts.Token);

        await WaitUntil(() => system.IsConnected, TimeSpan.FromSeconds(2));

        cts.Cancel();
        await startTask;
    }

    /// <summary>Start keeps retrying TryConnect on the configured interval while it keeps failing.</summary>
    [Fact]
    public async Task Start_TryConnectFails_RetriesOnInterval()
    {
        int attempts = 0;
        FakeExternalSystem system = new(TimeSpan.FromMilliseconds(20), TimeSpan.FromSeconds(30))
        {
            TryConnectImpl = _ => { attempts++; return Task.FromResult(false); }
        };
        using CancellationTokenSource cts = new();
        Task startTask = system.Start(cts.Token);

        await WaitUntil(() => attempts >= 3, TimeSpan.FromSeconds(2));

        Assert.False(system.IsConnected);
        cts.Cancel();
        await startTask;
    }

    /// <summary>An exception from TryConnect is treated as a failed connection attempt, not an unhandled crash.</summary>
    [Fact]
    public async Task Start_TryConnectThrows_TreatedAsFailedAttempt()
    {
        FakeExternalSystem system = new(TimeSpan.FromMilliseconds(20), TimeSpan.FromSeconds(30))
        {
            TryConnectImpl = _ => throw new InvalidOperationException("boom")
        };
        using CancellationTokenSource cts = new();
        Task startTask = system.Start(cts.Token);

        await Task.Delay(100);
        Assert.False(system.IsConnected);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Once connected, a failed poll transitions IsConnected back to false and calls Disconnect.</summary>
    [Fact]
    public async Task Start_PollDetectsDisconnect_BecomesDisconnectedAndCallsDisconnect()
    {
        FakeExternalSystem system = new(TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20))
        {
            PollIsConnectedImpl = _ => Task.FromResult(false)
        };
        using CancellationTokenSource cts = new();
        Task startTask = system.Start(cts.Token);

        await WaitUntil(() => system.DisconnectCallCount > 0, TimeSpan.FromSeconds(2));

        cts.Cancel();
        await startTask;
    }

    /// <summary>After a poll-detected disconnect, Start attempts to reconnect.</summary>
    [Fact]
    public async Task Start_AfterDisconnect_AttemptsReconnect()
    {
        int connectAttempts = 0;
        FakeExternalSystem system = new(TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20))
        {
            TryConnectImpl = _ => { connectAttempts++; return Task.FromResult(true); },
            PollIsConnectedImpl = _ => Task.FromResult(false)
        };
        using CancellationTokenSource cts = new();
        Task startTask = system.Start(cts.Token);

        await WaitUntil(() => connectAttempts >= 2, TimeSpan.FromSeconds(2));

        cts.Cancel();
        await startTask;
    }

    /// <summary>Send returns false immediately, without calling the abstract Send, while not connected.</summary>
    [Fact]
    public async Task Send_NotConnected_ReturnsFalseWithoutSending()
    {
        FakeExternalSystem system = new(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        bool result = await ((IExternalSystem)system).Send(new TestMessage { MessageId = "M1" });

        Assert.False(result);
        Assert.Empty(system.SentMessages);
    }

    /// <summary>Send calls the abstract Send and returns its result while connected.</summary>
    [Fact]
    public async Task Send_Connected_CallsAbstractSend()
    {
        FakeExternalSystem system = new(TimeSpan.FromMilliseconds(20), TimeSpan.FromSeconds(30));
        using CancellationTokenSource cts = new();
        Task startTask = system.Start(cts.Token);
        await WaitUntil(() => system.IsConnected, TimeSpan.FromSeconds(2));

        TestMessage message = new() { MessageId = "M1" };
        bool result = await ((IExternalSystem)system).Send(message);

        Assert.True(result);
        Assert.Single(system.SentMessages);
        Assert.Same(message, system.SentMessages[0]);

        cts.Cancel();
        await startTask;
    }

    /// <summary>An exception from the abstract Send is caught and Send returns false instead of throwing.</summary>
    [Fact]
    public async Task Send_AbstractSendThrows_ReturnsFalse()
    {
        FakeExternalSystem system = new(TimeSpan.FromMilliseconds(20), TimeSpan.FromSeconds(30))
        {
            SendImpl = _ => throw new InvalidOperationException("boom")
        };
        using CancellationTokenSource cts = new();
        Task startTask = system.Start(cts.Token);
        await WaitUntil(() => system.IsConnected, TimeSpan.FromSeconds(2));

        bool result = await ((IExternalSystem)system).Send(new TestMessage { MessageId = "M1" });

        Assert.False(result);

        cts.Cancel();
        await startTask;
    }

    /// <summary>A message a derived class reports via Receive is eventually delivered to MessageReceived with that same instance.</summary>
    [Fact]
    public async Task Receive_RaisesMessageReceived()
    {
        FakeExternalSystem system = new(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        object? received = null;
        ((IExternalSystem)system).MessageReceived += message => { received = message; return Task.CompletedTask; };

        using CancellationTokenSource cts = new();
        Task startTask = system.Start(cts.Token);

        TestMessage sent = new() { MessageId = "M1" };
        await system.Deliver(sent);

        await WaitUntil(() => received is not null, TimeSpan.FromSeconds(2));
        Assert.Same(sent, received);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Receive with no subscriber does not throw.</summary>
    [Fact]
    public async Task Receive_NoSubscriber_DoesNotThrow()
    {
        FakeExternalSystem system = new(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        using CancellationTokenSource cts = new();
        Task startTask = system.Start(cts.Token);

        await system.Deliver(new TestMessage { MessageId = "M1" });
        await Task.Delay(50);

        cts.Cancel();
        await startTask;
    }

    /// <summary>Multiple Receive calls made concurrently, without awaiting each one's own delivery first, are still all delivered — one message at a time, never overlapping, in the order they were enqueued.</summary>
    [Fact]
    public async Task Receive_ConcurrentCalls_DeliveredSequentiallyInOrder()
    {
        FakeExternalSystem system = new(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        int concurrentDeliveries = 0;
        int maxObservedConcurrency = 0;
        List<string> deliveredIds = [];
        ((IExternalSystem)system).MessageReceived += async message =>
        {
            int current = Interlocked.Increment(ref concurrentDeliveries);
            InterlockedMax(ref maxObservedConcurrency, current);
            await Task.Delay(20);
            lock (deliveredIds) { deliveredIds.Add(((TestMessage)message).MessageId); }
            Interlocked.Decrement(ref concurrentDeliveries);
        };

        using CancellationTokenSource cts = new();
        Task startTask = system.Start(cts.Token);

        TestMessage[] messages = [.. Enumerable.Range(0, 5).Select(i => new TestMessage { MessageId = $"M{i}" })];
        await Task.WhenAll(messages.Select(system.Deliver));

        await WaitUntil(() => deliveredIds.Count == messages.Length, TimeSpan.FromSeconds(5));

        Assert.Equal(1, maxObservedConcurrency);
        Assert.Equal(messages.Select(m => m.MessageId), deliveredIds);

        cts.Cancel();
        await startTask;
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        do { current = target; }
        while (value > current && Interlocked.CompareExchange(ref target, value, current) != current);
    }

    /// <summary>A derived class that does not override PollIsConnected relies on the base default, which never reports a lost connection on its own.</summary>
    [Fact]
    public async Task PollIsConnected_NotOverridden_DefaultsToAlwaysConnected()
    {
        NonPollingFakeExternalSystem system = new(TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));
        using CancellationTokenSource cts = new();
        Task startTask = system.Start(cts.Token);

        await WaitUntil(() => system.IsConnected, TimeSpan.FromSeconds(2));
        await Task.Delay(150);

        Assert.True(system.IsConnected);
        Assert.Equal(0, system.DisconnectCallCount);

        cts.Cancel();
        await startTask;
    }

    /// <summary>ReportDisconnected while connected transitions to disconnected and calls Disconnect promptly, without waiting for the (much longer) poll interval.</summary>
    [Fact]
    public async Task ReportDisconnected_WhileConnected_TransitionsToDisconnectedPromptly()
    {
        NonPollingFakeExternalSystem system = new(TimeSpan.FromMilliseconds(20), TimeSpan.FromSeconds(30));
        using CancellationTokenSource cts = new();
        Task startTask = system.Start(cts.Token);

        await WaitUntil(() => system.IsConnected, TimeSpan.FromSeconds(2));

        system.SimulateDisconnect();

        await WaitUntil(() => system.DisconnectCallCount > 0, TimeSpan.FromSeconds(1));

        cts.Cancel();
        await startTask;
    }

    /// <summary>ReportDisconnected called while never connected is a no-op that does not throw.</summary>
    [Fact]
    public void ReportDisconnected_NotConnected_DoesNotThrow()
    {
        NonPollingFakeExternalSystem system = new(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        system.SimulateDisconnect();
    }

    /// <summary>ReportDisconnected called after Start has already stopped does not throw.</summary>
    [Fact]
    public async Task ReportDisconnected_AfterStartStopped_DoesNotThrow()
    {
        NonPollingFakeExternalSystem system = new(TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));
        using CancellationTokenSource cts = new();
        Task startTask = system.Start(cts.Token);
        await WaitUntil(() => system.IsConnected, TimeSpan.FromSeconds(2));

        cts.Cancel();
        await startTask;

        system.SimulateDisconnect();
    }
}
