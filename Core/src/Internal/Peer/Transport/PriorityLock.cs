namespace BlueHeighliner.Comlink;

/// <summary>An asynchronous mutual exclusion lock that hands itself to the waiter with the highest priority first, first come, first served among equals.</summary>
internal interface IPriorityLock
{
    /// <summary>Waits for the lock.</summary>
    /// <param name="priority">The waiter's priority; larger values are granted first.</param>
    /// <param name="cancellation">Cancels the wait.</param>
    /// <returns>A lease that releases the lock when disposed.</returns>
    /// <exception cref="OperationCanceledException">The wait was cancelled.</exception>
    Task<IDisposable> Acquire(int priority, CancellationToken cancellation);
}

/// <inheritdoc cref="IPriorityLock" />
internal sealed class PriorityLock : IPriorityLock
{
    private readonly Lock gate = new();
    private readonly PriorityQueue<TaskCompletionSource<IDisposable>, (long NegatedPriority, long Sequence)> waiters = new();
    private long sequence;
    private bool held;

    /// <inheritdoc />
    public Task<IDisposable> Acquire(int priority, CancellationToken cancellation)
    {
        TaskCompletionSource<IDisposable> waiter = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (gate)
        {
            if (!held)
            {
                held = true;
                return Task.FromResult<IDisposable>(new Lease(this));
            }

            waiters.Enqueue(waiter, (-(long)priority, sequence++));
        }

        if (cancellation.CanBeCanceled) { cancellation.Register(() => waiter.TrySetCanceled(cancellation)); }
        return waiter.Task;
    }

    private void Release()
    {
        lock (gate)
        {
            while (waiters.TryDequeue(out TaskCompletionSource<IDisposable>? next, out _))
            {
                if (next.TrySetResult(new Lease(this))) { return; }
            }

            held = false;
        }
    }

    private sealed class Lease(PriorityLock owner) : IDisposable
    {
        private int released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) == 0) { owner.Release(); }
        }
    }
}
