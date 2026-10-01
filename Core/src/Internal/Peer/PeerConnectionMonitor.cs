namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// Proactively opens, and continuously maintains, the connection to one of this node's outgoing
/// <see cref="ConnectionPoint"/>s by connecting to it and requesting a heartbeat over the connection,
/// awaiting its outcome. The heartbeat is an empty instance of the message type, serialized like any message (see
/// <see cref="EngineControllerExtensions.IsHeartbeat"/>), so nothing but serialized messages and packets ever crosses a connection;
/// <see cref="PeerMessageDispatcher.Dispatch"/> and <see cref="ServerRoutingService"/> acknowledge it without acting on it, so it never
/// reaches the remote peer's application logic. Over IP a heartbeat reuses the
/// same cached session connection, so the connection genuinely stays open between heartbeats rather than flapping; over
/// serial the first connect is what creates the link to the port, which then reconnects on its own.
/// </summary>
/// <remarks>
/// While the last heartbeat did not succeed - including the very first one, which commonly races the
/// remote peer's own receiver still starting up (a fresh <c>ECONNREFUSED</c> is near-instant, not a slow
/// timeout) - heartbeats retry on <see cref="fastRetryInterval"/> instead of <see cref="steadyInterval"/>,
/// so a transient startup race resolves within a couple of seconds rather than sitting on a hierarchical
/// connection's status table as incorrectly "down" for up to a full <see cref="steadyInterval"/>. The same
/// fast retry re-engages immediately after any later drop. The connection's live state is then observed the
/// normal way, through <see cref="IPeerTransport.Connected"/>/<see cref="IPeerTransport.Disconnected"/>, by
/// whichever caller subscribes to those observables. See <c>Docs/Components/Peer.md</c>.
/// </remarks>
internal sealed class PeerConnectionMonitor(IEngineController engineController, TimeSpan? steadyInterval = null, TimeSpan? fastRetryInterval = null)
{
    private readonly TimeSpan steadyInterval = steadyInterval ?? TimeSpan.FromSeconds(30);
    private readonly TimeSpan fastRetryInterval = fastRetryInterval ?? TimeSpan.FromSeconds(2);

    /// <summary>
    /// Starts connecting to <paramref name="target"/> through <paramref name="transport"/> and requesting a
    /// heartbeat over the connection in the background, immediately and then repeatedly - on <see cref="fastRetryInterval"/>
    /// while disconnected, on <see cref="steadyInterval"/> once connected - until <paramref
    /// name="cancellation"/> is triggered.
    /// </summary>
    /// <param name="transport">The transport to send heartbeats through.</param>
    /// <param name="target">The point to maintain a connection to.</param>
    /// <param name="cancellation">Stops sending heartbeats.</param>
    /// <param name="acknowledged">
    /// Invoked with the connection each time the remote node acknowledges a heartbeat. A connection only counts as up once a heartbeat has
    /// been acknowledged: a remote node that has closed the connection accepts it and drops it again without ever
    /// answering, and treating the bare connection as up would flash it green every time.
    /// </param>
    /// <returns>A handle that can pause, resume, or immediately trigger the heartbeat loop.</returns>
    public PeerLinkControl Maintain(IPeerTransport transport, ConnectionPoint target, CancellationToken cancellation, Action<PeerConnection>? acknowledged = null)
    {
        PeerLinkControl control = new();
        _ = Task.Run(() => Loop(transport, target, control, acknowledged, cancellation), cancellation);
        return control;
    }

    private async Task Loop(IPeerTransport transport, ConnectionPoint target, PeerLinkControl control, Action<PeerConnection>? acknowledged, CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            TimeSpan delay = Timeout.InfiniteTimeSpan;
            if (!control.IsClosed)
            {
                bool connected;
                PeerConnection? connection = null;
                try
                {
                    connection = await transport.Connect(target, cancellation);
                    byte[] heartbeat;
                    using (IMemoryOwner<byte> owner = engineController.NetworkSerializer.Serialize(engineController.CreateMessage())) { heartbeat = owner.Memory.ToArray(); }
                    connected = await transport.Request(connection, heartbeat, new PeerSendOptions { Priority = int.MinValue }, cancellation);
                }
                catch
                {
                    connected = false;
                }

                delay = connected ? steadyInterval : fastRetryInterval;
                control.ReportOutcome(connected);
                if (connected && connection is not null)
                {
                    try { acknowledged?.Invoke(connection); }
                    catch { }
                }
            }

            try { await control.Wait(delay, cancellation); }
            catch (OperationCanceledException) { return; }
        }
    }
}
