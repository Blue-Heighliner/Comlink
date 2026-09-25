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
