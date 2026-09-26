namespace BlueHeighliner.Comlink.Tests.Unit.Peer;

/// <summary>Unit tests for <see cref="PeerConnectionMonitor"/>.</summary>
public sealed class PeerConnectionMonitorTests
{
    private static readonly UserEndpoint target = new() { IpAddress = "10.0.0.1", Port = 9000 };

    private static void AutoAcknowledge(Mock<IPeerTransport> transport, bool success = true)
        => transport.Setup(t => t.Request(It.IsAny<UserEndpoint>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(success);

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) { throw new TimeoutException("Condition was not met in time."); }
            await Task.Delay(10);
        }
    }

    /// <summary>Maintain sends an empty heartbeat immediately, without waiting for the first interval to elapse.</summary>
    [Fact]
    public async Task Maintain_SendsHeartbeatImmediately()
    {
        Mock<IPeerTransport> transport = new();
        AutoAcknowledge(transport);
        PeerConnectionMonitor monitor = new(steadyInterval: TimeSpan.FromMinutes(10), fastRetryInterval: TimeSpan.FromMinutes(10));

        using CancellationTokenSource cts = new();
        monitor.Maintain(transport.Object, target, cts.Token);

        await WaitUntil(
            () => transport.Invocations.Any(i => i.Method.Name == nameof(IPeerTransport.Request) && ((ReadOnlyMemory<byte>)i.Arguments[1]).Length == 0),
            TimeSpan.FromSeconds(2));

        cts.Cancel();
    }

    /// <summary>While the last heartbeat failed, the next one follows the fast retry interval rather than the steady one.</summary>
    [Fact]
    public async Task Maintain_WhileDisconnected_RetriesOnFastInterval()
    {
        Mock<IPeerTransport> transport = new();
        transport.Setup(t => t.Request(It.IsAny<UserEndpoint>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("refused"));
        PeerConnectionMonitor monitor = new(steadyInterval: TimeSpan.FromMinutes(10), fastRetryInterval: TimeSpan.FromMilliseconds(50));

        using CancellationTokenSource cts = new();
        monitor.Maintain(transport.Object, target, cts.Token);

        await WaitUntil(
            () => transport.Invocations.Count(i => i.Method.Name == nameof(IPeerTransport.Request)) >= 3,
            TimeSpan.FromSeconds(2));

        cts.Cancel();
    }

    /// <summary>Once a heartbeat succeeds, the next one follows the steady interval rather than the fast retry one.</summary>
    [Fact]
    public async Task Maintain_OnceConnected_DoesNotRetryOnFastIntervalAgain()
    {
        Mock<IPeerTransport> transport = new();
        AutoAcknowledge(transport);
        PeerConnectionMonitor monitor = new(steadyInterval: TimeSpan.FromSeconds(30), fastRetryInterval: TimeSpan.FromMilliseconds(20));

        using CancellationTokenSource cts = new();
        monitor.Maintain(transport.Object, target, cts.Token);

        await WaitUntil(
            () => transport.Invocations.Any(i => i.Method.Name == nameof(IPeerTransport.Request)),
            TimeSpan.FromSeconds(2));

        int countAfterFirst = transport.Invocations.Count(i => i.Method.Name == nameof(IPeerTransport.Request));
        await Task.Delay(200);
        int countAfterWait = transport.Invocations.Count(i => i.Method.Name == nameof(IPeerTransport.Request));

        cts.Cancel();

        // With a 20ms fast interval, 200ms of extra waiting would have produced many more requests had the
        // monitor stayed on the fast interval instead of switching to the 30s steady one after connecting.
        Assert.Equal(countAfterFirst, countAfterWait);
    }

    private static int Heartbeats(Mock<IPeerTransport> transport) => transport.Invocations.Count(i => i.Method.Name == nameof(IPeerTransport.Request));

    /// <summary>Refresh makes the monitor heartbeat again straight away, without waiting out the steady interval.</summary>
    [Fact]
    public async Task Refresh_HeartbeatsImmediately()
    {
        Mock<IPeerTransport> transport = new();
        AutoAcknowledge(transport);
        PeerConnectionMonitor monitor = new(steadyInterval: TimeSpan.FromMinutes(10), fastRetryInterval: TimeSpan.FromMinutes(10));
        using CancellationTokenSource cts = new();
        PeerLinkControl control = monitor.Maintain(transport.Object, target, cts.Token);
        await WaitUntil(() => Heartbeats(transport) == 1, TimeSpan.FromSeconds(2));

        control.Refresh();

        await WaitUntil(() => Heartbeats(transport) == 2, TimeSpan.FromSeconds(2));
        cts.Cancel();
    }

    /// <summary>A closed monitor stops heartbeating, so nothing re-opens the connection, until it is opened again.</summary>
    [Fact]
    public async Task Close_StopsHeartbeats_OpenResumesImmediately()
    {
        Mock<IPeerTransport> transport = new();
        AutoAcknowledge(transport);
        PeerConnectionMonitor monitor = new(steadyInterval: TimeSpan.FromMilliseconds(20), fastRetryInterval: TimeSpan.FromMilliseconds(20));
        using CancellationTokenSource cts = new();
        PeerLinkControl control = monitor.Maintain(transport.Object, target, cts.Token);
        await WaitUntil(() => Heartbeats(transport) >= 2, TimeSpan.FromSeconds(2));

        control.Close();
        await Task.Delay(100);
        int whileClosed = Heartbeats(transport);
        await Task.Delay(200);

        Assert.True(control.IsClosed);
        Assert.Equal(whileClosed, Heartbeats(transport));

        control.Open();
        await WaitUntil(() => Heartbeats(transport) > whileClosed, TimeSpan.FromSeconds(2));
        Assert.False(control.IsClosed);
        cts.Cancel();
    }

    /// <summary>A monitor that starts out closed after being closed before its first heartbeat sends none.</summary>
    [Fact]
    public async Task Close_ImmediatelyAfterMaintain_SendsAtMostTheFirstHeartbeat()
    {
        Mock<IPeerTransport> transport = new();
        AutoAcknowledge(transport);
        PeerConnectionMonitor monitor = new(steadyInterval: TimeSpan.FromMilliseconds(20), fastRetryInterval: TimeSpan.FromMilliseconds(20));
        using CancellationTokenSource cts = new();
        PeerLinkControl control = monitor.Maintain(transport.Object, target, cts.Token);

        control.Close();
        await Task.Delay(100);
        int count = Heartbeats(transport);
        await Task.Delay(200);

        Assert.True(count <= 1);
        Assert.Equal(count, Heartbeats(transport));
        cts.Cancel();
    }

    /// <summary>The acknowledged callback fires for each acknowledged heartbeat, and never for a failed or negatively acknowledged one.</summary>
    [Fact]
    public async Task Maintain_AcknowledgedCallback_FiresOnlyOnSuccess()
    {
        Mock<IPeerTransport> failing = new();
        AutoAcknowledge(failing, success: false);
        Mock<IPeerTransport> throwing = new();
        throwing.Setup(t => t.Request(It.IsAny<UserEndpoint>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException());
        Mock<IPeerTransport> working = new();
        AutoAcknowledge(working);
        PeerConnectionMonitor monitor = new(steadyInterval: TimeSpan.FromMilliseconds(20), fastRetryInterval: TimeSpan.FromMilliseconds(20));
        using CancellationTokenSource cts = new();
        int failedAcks = 0;
        int thrownAcks = 0;
        int acks = 0;

        monitor.Maintain(failing.Object, target, cts.Token, () => failedAcks++);
        monitor.Maintain(throwing.Object, target, cts.Token, () => thrownAcks++);
        monitor.Maintain(working.Object, target, cts.Token, () => acks++);
        await WaitUntil(() => acks >= 3, TimeSpan.FromSeconds(2));

        Assert.Equal(0, failedAcks);
        Assert.Equal(0, thrownAcks);
        cts.Cancel();
    }

    /// <summary>A callback that throws does not stop the heartbeat loop.</summary>
    [Fact]
    public async Task Maintain_AcknowledgedCallbackThrows_LoopContinues()
    {
        Mock<IPeerTransport> transport = new();
        AutoAcknowledge(transport);
        PeerConnectionMonitor monitor = new(steadyInterval: TimeSpan.FromMilliseconds(20), fastRetryInterval: TimeSpan.FromMilliseconds(20));
        using CancellationTokenSource cts = new();

        monitor.Maintain(transport.Object, target, cts.Token, () => throw new InvalidOperationException());

        await WaitUntil(() => Heartbeats(transport) >= 3, TimeSpan.FromSeconds(2));
        cts.Cancel();
    }

    /// <summary>A negatively-acknowledged heartbeat (Success = false) is treated as disconnected, following the fast retry interval.</summary>
    [Fact]
    public async Task Maintain_NegativelyAcknowledged_RetriesOnFastInterval()
    {
        Mock<IPeerTransport> transport = new();
        AutoAcknowledge(transport, success: false);
        PeerConnectionMonitor monitor = new(steadyInterval: TimeSpan.FromMinutes(10), fastRetryInterval: TimeSpan.FromMilliseconds(50));

        using CancellationTokenSource cts = new();
        monitor.Maintain(transport.Object, target, cts.Token);

        await WaitUntil(
            () => transport.Invocations.Count(i => i.Method.Name == nameof(IPeerTransport.Request)) >= 3,
            TimeSpan.FromSeconds(2));

        cts.Cancel();
    }
}
