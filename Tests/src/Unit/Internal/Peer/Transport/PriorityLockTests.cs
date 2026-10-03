namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer.Transport;

/// <summary>Tests for <see cref="PriorityLock"/>.</summary>
public sealed class PriorityLockTests
{
    /// <summary>Waiters are granted the lock highest priority first, and in arrival order among equals.</summary>
    [Fact]
    public async Task Acquire_GrantsHighestPriorityFirst()
    {
        PriorityLock sut = new();
        IDisposable first = await sut.Acquire(0, CancellationToken.None);
        List<string> order = [];

        async Task Wait(string name, int priority)
        {
            using (await sut.Acquire(priority, CancellationToken.None)) { lock (order) { order.Add(name); } }
        }

        Task low = Wait("low", 1);
        Task highA = Wait("highA", 5);
        Task highB = Wait("highB", 5);
        first.Dispose();
        await Task.WhenAll(low, highA, highB);

        Assert.Equal(["highA", "highB", "low"], order);
    }

    /// <summary>A cancelled waiter is skipped and does not block the lock.</summary>
    [Fact]
    public async Task Acquire_CancelledWaiter_IsSkipped()
    {
        PriorityLock sut = new();
        IDisposable first = await sut.Acquire(0, CancellationToken.None);
        using CancellationTokenSource cancel = new();
        Task<IDisposable> cancelled = sut.Acquire(9, cancel.Token);
        Task<IDisposable> next = sut.Acquire(1, CancellationToken.None);

        await cancel.CancelAsync();
        first.Dispose();

        await Assert.ThrowsAsync<TaskCanceledException>(() => cancelled);
        (await next.WaitAsync(TimeSpan.FromSeconds(5))).Dispose();
    }
}
