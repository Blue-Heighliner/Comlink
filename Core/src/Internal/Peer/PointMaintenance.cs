namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// Keeps the set of outgoing points a service maintains connections to in line with the configuration as it changes: a point that is newly defined gets a
/// <see cref="PeerConnectionMonitor"/> loop, a point that is no longer defined (or whose user changed) has its loop cancelled and its connection dropped,
/// and a point that has not changed is left alone, so its connection is never touched.
/// </summary>
internal sealed class PointMaintenance(PeerConnectionMonitor monitor)
{
    private readonly Dictionary<string, Entry> entries = [];
    private readonly HashSet<string> closed = [];
    private readonly Lock gate = new();

    /// <summary>
    /// Brings the maintained points to <paramref name="desired"/>. Returns what was removed and what was started, each once per point, so the caller can
    /// update whatever else it tracks per point.
    /// </summary>
    /// <param name="transport">The transport the connections are made over.</param>
    /// <param name="desired">The points that should be maintained now; a point names the same place as another when their keys are equal.</param>
    /// <param name="lifetime">Cancelled when the service stops, which ends every loop.</param>
    /// <param name="acknowledged">Called with a connection each time its point's heartbeat is acknowledged.</param>
    public (IReadOnlyList<ConnectionPoint> Removed, IReadOnlyList<(ConnectionPoint Point, PeerLinkControl Control)> Started) Sync(
        IPeerTransport transport,
        IEnumerable<ConnectionPoint> desired,
        CancellationToken lifetime,
        Action<PeerConnection>? acknowledged = null)
    {
        Dictionary<string, ConnectionPoint> wanted = [];
        foreach (ConnectionPoint point in desired) { wanted.TryAdd(point.Key, point); }

        List<ConnectionPoint> removed = [];
        List<(ConnectionPoint, PeerLinkControl)> started = [];
        lock (gate)
        {
            foreach ((string key, Entry entry) in entries.ToList())
            {
                if (wanted.TryGetValue(key, out ConnectionPoint? point) && point.User == entry.Point.User) { continue; }

                entry.Cancel.Cancel();
                entry.Cancel.Dispose();
                transport.SetClosed(entry.Point, true);
                closed.Add(key);
                entries.Remove(key);
                removed.Add(entry.Point);
            }

            foreach ((string key, ConnectionPoint point) in wanted)
            {
                if (entries.ContainsKey(key)) { continue; }

                if (closed.Remove(key)) { transport.SetClosed(point, false); }
                CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
                PeerLinkControl control = monitor.Maintain(transport, point, cancel.Token, acknowledged);
                entries[key] = new Entry(point, cancel);
                started.Add((point, control));
            }
        }

        return (removed, started);
    }

    private sealed record Entry(ConnectionPoint Point, CancellationTokenSource Cancel);
}
