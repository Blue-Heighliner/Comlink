namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// Handle on one <see cref="PeerConnectionMonitor"/> loop, letting a service pause it (closed: no heartbeats, so nothing
/// opens the connection), resume it, or make it heartbeat right now instead of waiting out its interval.
/// </summary>
internal sealed class PeerLinkControl
{
    private readonly SemaphoreSlim wake = new(0, 1);
    private volatile bool isClosed;

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

    // Repeated presses while the monitor is busy collapse into a single wake-up rather than queuing one each.
    private void Wake()
    {
        try { wake.Release(); }
        catch (SemaphoreFullException) { }
    }

    /// <summary>Waits up to <paramref name="timeout"/>, returning early when the monitor is closed, opened, or refreshed.</summary>
    public Task<bool> Wait(TimeSpan timeout, CancellationToken cancellation) => wake.WaitAsync(timeout, cancellation);
}
