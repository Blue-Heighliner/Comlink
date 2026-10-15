namespace BlueHeighliner.Comlink;

/// <summary>
/// Handle on one <see cref="PeerConnectionMonitor"/> loop, letting a service pause it (closed: no heartbeats, so nothing
/// opens the connection), resume it, or make it heartbeat right now instead of waiting out its interval.
/// </summary>
internal sealed class PeerLinkControl
{
    private readonly SemaphoreSlim wake = new(0, 1);
    private volatile bool isClosed;
    private volatile bool isRetrying;
    private volatile bool isAttempting;
    private long lostAt;

    /// <summary>Gets a value indicating whether the monitor is paused.</summary>
    public bool IsClosed => isClosed;

    /// <summary>Pauses the monitor until <see cref="Open"/>.</summary>
    public void Close()
    {
        isClosed = true;
        Wake();
    }

    /// <summary>Resumes a paused monitor, which heartbeats immediately.</summary>
    public void Open()
    {
        isClosed = false;
        Wake();
    }

    /// <summary>Makes the monitor heartbeat now instead of waiting for its next interval.</summary>
    public void Refresh() => Wake();

    /// <summary>
    /// Records whether the last heartbeat succeeded, which decides whether <see cref="NotifyLost"/> has anything to do.
    /// </summary>
    public void ReportOutcome(bool succeeded) => isRetrying = !succeeded;

    /// <summary>Marks the start of a connection attempt, which <see cref="NotifyLost"/> then leaves to finish and judge by itself, and returns the time to ask <see cref="WasLostSince"/> about.</summary>
    public long BeginAttempt()
    {
        isAttempting = true;
        return Environment.TickCount64;
    }

    /// <summary>Marks the end of the attempt begun by <see cref="BeginAttempt"/>, once its outcome is reported.</summary>
    public void EndAttempt() => isAttempting = false;

    /// <summary>Returns whether the connection was reported lost at or after <paramref name="mark"/>, the value <see cref="BeginAttempt"/> returned.</summary>
    /// <param name="mark">The time an attempt began.</param>
    public bool WasLostSince(long mark) => Volatile.Read(ref lostAt) is var at && at != 0 && at >= mark;

    /// <summary>
    /// Tells the monitor that its connection was lost. If the last heartbeat succeeded the monitor is asleep for its
    /// whole steady interval and would not notice for that long, so it is woken to retry at once. If it was already
    /// failing it is already retrying on the short interval, and if it is in the middle of an attempt it will see the
    /// loss itself (see <see cref="WasLostSince"/>) and wait out the retry interval: waking it either way would make every attempt that
    /// connects and is dropped straight away (a server that has refused the connection does exactly that) start the next at once,
    /// with nothing to slow it down.
    /// </summary>
    public void NotifyLost()
    {
        Volatile.Write(ref lostAt, Environment.TickCount64);
        if (!isRetrying && !isAttempting)
        {
            Wake();
        }
    }

    // Repeated presses while the monitor is busy collapse into a single wake-up rather than queuing one each.
    private void Wake()
    {
        try { wake.Release(); }
        catch (SemaphoreFullException) { }
    }

    /// <summary>Waits up to <paramref name="timeout"/>, returning early when the monitor is closed, opened, or refreshed.</summary>
    public Task<bool> Wait(TimeSpan timeout, CancellationToken cancellation) => wake.WaitAsync(timeout, cancellation);
}
