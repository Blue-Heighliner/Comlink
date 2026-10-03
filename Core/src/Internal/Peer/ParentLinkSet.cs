namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// The points a client or relay dials to reach its parent and the heartbeat controls of their monitors. An MSMT parent is one point; an HDLC parent is one per port the node opens, since
/// nothing says which port is cabled to it, so closing, refreshing and noticing a loss all act on every one.
/// </summary>
internal sealed class ParentLinkSet
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, ConnectionPoint> points = [];
    private readonly Dictionary<string, PeerLinkControl> controls = [];

    /// <summary>Whether the parent is reached by dialing at all, which is not so when its link is listen mode.</summary>
    public bool HasPoints
    {
        get { lock (gate) { return points.Count > 0; } }
    }

    /// <summary>Whether <paramref name="point"/> is one of the points that reach the parent.</summary>
    /// <param name="point">The point a connection was dialed to, or <see langword="null"/> for an inbound one.</param>
    public bool Contains(ConnectionPoint? point)
    {
        if (point is null) { return false; }

        lock (gate) { return points.ContainsKey(point.Key); }
    }

    /// <summary>Records <paramref name="point"/> as one that reaches the parent, before its loop starts.</summary>
    /// <param name="point">The point about to be dialed.</param>
    public void Track(ConnectionPoint point)
    {
        lock (gate) { points[point.Key] = point; }
    }

    /// <summary>Records the controls of the monitors just started.</summary>
    /// <param name="started">Each started point and its control.</param>
    public void Attach(IEnumerable<(ConnectionPoint Point, PeerLinkControl Control)> started)
    {
        lock (gate)
        {
            foreach ((ConnectionPoint point, PeerLinkControl control) in started) { controls[point.Key] = control; }
        }
    }

    /// <summary>Forgets the points that are no longer dialed.</summary>
    /// <param name="removed">The points whose monitors were stopped.</param>
    public void Remove(IEnumerable<ConnectionPoint> removed)
    {
        lock (gate)
        {
            foreach (ConnectionPoint point in removed)
            {
                points.Remove(point.Key);
                controls.Remove(point.Key);
            }
        }
    }

    /// <summary>Forgets every point.</summary>
    public void Clear()
    {
        lock (gate)
        {
            points.Clear();
            controls.Clear();
        }
    }

    /// <summary>Closes or reopens every point of the parent link.</summary>
    /// <param name="transport">The transport that dials them.</param>
    /// <param name="closed">Whether to close.</param>
    public void SetClosed(IPeerTransport transport, bool closed)
    {
        foreach ((ConnectionPoint point, PeerLinkControl? control) in Snapshot())
        {
            transport.SetClosed(point, closed);
            if (closed) { control?.Close(); }
            else { control?.Open(); }
        }
    }

    /// <summary>Resets every point and has its monitor heartbeat again at once.</summary>
    /// <param name="transport">The transport that dials them.</param>
    public void Refresh(IPeerTransport transport)
    {
        foreach ((ConnectionPoint point, PeerLinkControl? control) in Snapshot())
        {
            transport.Reset(point);
            control?.Refresh();
        }
    }

    /// <summary>Wakes every monitor so it retries without waiting out its interval.</summary>
    public void NotifyLost()
    {
        foreach ((_, PeerLinkControl? control) in Snapshot()) { control?.NotifyLost(); }
    }

    private List<(ConnectionPoint Point, PeerLinkControl? Control)> Snapshot()
    {
        lock (gate) { return [.. points.Select(entry => (entry.Value, controls.GetValueOrDefault(entry.Key)))]; }
    }
}
