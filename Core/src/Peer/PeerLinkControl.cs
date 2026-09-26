namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// Handle on one <see cref="PeerConnectionMonitor"/> loop, letting a service pause it (closed: no heartbeats, so nothing
/// opens the connection), resume it, or make it heartbeat right now instead of waiting out its interval.
/// </summary>
internal sealed class PeerLinkControl
{
    private readonly SemaphoreSlim wake = new(0);

    /// <summary>Gets a value indicating whether the monitor is paused.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Pauses the monitor until <see cref="Open"/>.</summary>
    public void Close()
    {
        IsClosed = true;
        wake.Release();
    }

    /// <summary>Resumes a paused monitor, which heartbeats immediately.</summary>
    public void Open()
    {
        IsClosed = false;
        wake.Release();
    }

    /// <summary>Makes the monitor heartbeat now instead of waiting for its next interval.</summary>
    public void Refresh() => wake.Release();

    /// <summary>Waits up to <paramref name="timeout"/>, returning early when the monitor is closed, opened, or refreshed.</summary>
    public Task<bool> Wait(TimeSpan timeout, CancellationToken cancellation) => wake.WaitAsync(timeout, cancellation);
}
