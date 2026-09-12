namespace BlueHeighliner.Comlink.Tests.Peer;

/// <summary>Unit tests for <see cref="MsmtConnectionMonitor"/>.</summary>
public sealed class MsmtConnectionMonitorTests
{
    private static readonly MsmtNameTarget target = new() { Host = "10.0.0.1", Port = 9000, ServerName = "10.0.0.1" };

    private static void AutoAcknowledge(Mock<IMsmtPeer> peer, bool success = true)
        => peer.Setup(p => p.Request(It.IsAny<MsmtNameTarget>(), It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MsmtResponse { Success = success, Payload = new UnownedMemory(ReadOnlyMemory<byte>.Empty) });

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
        Mock<IMsmtPeer> peer = new();
        AutoAcknowledge(peer);
        MsmtConnectionMonitor monitor = new(steadyInterval: TimeSpan.FromMinutes(10), fastRetryInterval: TimeSpan.FromMinutes(10));

        using CancellationTokenSource cts = new();
        monitor.Maintain(peer.Object, target, cts.Token);

        await WaitUntil(
            () => peer.Invocations.Any(i => i.Method.Name == nameof(IMsmtPeer.Request) && ((IMemoryOwner<byte>)i.Arguments[1]).Memory.Length == 0),
            TimeSpan.FromSeconds(2));

        cts.Cancel();
    }

    /// <summary>While the last heartbeat failed, the next one follows the fast retry interval rather than the steady one.</summary>
    [Fact]
    public async Task Maintain_WhileDisconnected_RetriesOnFastInterval()
    {
        Mock<IMsmtPeer> peer = new();
        peer.Setup(p => p.Request(It.IsAny<MsmtNameTarget>(), It.IsAny<IMemoryOwner<byte>>(), It.IsAny<MsmtSendOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SocketException((int)SocketError.ConnectionRefused));
        MsmtConnectionMonitor monitor = new(steadyInterval: TimeSpan.FromMinutes(10), fastRetryInterval: TimeSpan.FromMilliseconds(50));

        using CancellationTokenSource cts = new();
        monitor.Maintain(peer.Object, target, cts.Token);

        await WaitUntil(
            () => peer.Invocations.Count(i => i.Method.Name == nameof(IMsmtPeer.Request)) >= 3,
            TimeSpan.FromSeconds(2));

        cts.Cancel();
    }

    /// <summary>Once a heartbeat succeeds, the next one follows the steady interval rather than the fast retry one.</summary>
    [Fact]
    public async Task Maintain_OnceConnected_DoesNotRetryOnFastIntervalAgain()
    {
        Mock<IMsmtPeer> peer = new();
        AutoAcknowledge(peer);
        MsmtConnectionMonitor monitor = new(steadyInterval: TimeSpan.FromSeconds(30), fastRetryInterval: TimeSpan.FromMilliseconds(20));

        using CancellationTokenSource cts = new();
        monitor.Maintain(peer.Object, target, cts.Token);

        await WaitUntil(
            () => peer.Invocations.Any(i => i.Method.Name == nameof(IMsmtPeer.Request)),
            TimeSpan.FromSeconds(2));

        int countAfterFirst = peer.Invocations.Count(i => i.Method.Name == nameof(IMsmtPeer.Request));
        await Task.Delay(200);
        int countAfterWait = peer.Invocations.Count(i => i.Method.Name == nameof(IMsmtPeer.Request));

        cts.Cancel();

        // With a 20ms fast interval, 200ms of extra waiting would have produced many more requests had the
        // monitor stayed on the fast interval instead of switching to the 30s steady one after connecting.
        Assert.Equal(countAfterFirst, countAfterWait);
    }

    /// <summary>A negatively-acknowledged heartbeat (Success = false) is treated as disconnected, following the fast retry interval.</summary>
    [Fact]
    public async Task Maintain_NegativelyAcknowledged_RetriesOnFastInterval()
    {
        Mock<IMsmtPeer> peer = new();
        AutoAcknowledge(peer, success: false);
        MsmtConnectionMonitor monitor = new(steadyInterval: TimeSpan.FromMinutes(10), fastRetryInterval: TimeSpan.FromMilliseconds(50));

        using CancellationTokenSource cts = new();
        monitor.Maintain(peer.Object, target, cts.Token);

        await WaitUntil(
            () => peer.Invocations.Count(i => i.Method.Name == nameof(IMsmtPeer.Request)) >= 3,
            TimeSpan.FromSeconds(2));

        cts.Cancel();
    }

    private sealed class UnownedMemory(ReadOnlyMemory<byte> data) : IMemoryOwner<byte>
    {
        public Memory<byte> Memory { get; } = data.ToArray();
        public void Dispose() { }
    }
}
