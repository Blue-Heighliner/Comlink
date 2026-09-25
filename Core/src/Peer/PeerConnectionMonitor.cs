namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// Proactively opens, and continuously maintains, a connection to each of a Client or Server role's
/// hierarchical peers (a Client's own server, or a Server's own children and sibling servers) by requesting
/// an empty heartbeat payload and awaiting its outcome - <see cref="PeerMessageDispatcher.Dispatch"/> and
/// <see cref="ServerRoutingService"/> both treat an empty payload as a no-op, so it never reaches the remote
/// peer's application logic. Over IP a heartbeat reuses the same cached session connection a real send would
/// create, so the connection genuinely stays open between heartbeats rather than flapping; over serial a
/// heartbeat is what first opens the link to the port, which then reconnects on its own.
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
internal sealed class PeerConnectionMonitor(TimeSpan? steadyInterval = null, TimeSpan? fastRetryInterval = null)
{
    private readonly TimeSpan steadyInterval = steadyInterval ?? TimeSpan.FromSeconds(30);
    private readonly TimeSpan fastRetryInterval = fastRetryInterval ?? TimeSpan.FromSeconds(2);

    /// <summary>
    /// Starts requesting an empty heartbeat payload from <paramref name="target"/> through <paramref
    /// name="transport"/> in the background, immediately and then repeatedly - on <see cref="fastRetryInterval"/>
    /// while disconnected, on <see cref="steadyInterval"/> once connected - until <paramref
    /// name="cancellation"/> is triggered.
    /// </summary>
    /// <param name="transport">The transport to send heartbeats through.</param>
    /// <param name="target">The hierarchical peer to maintain a connection to.</param>
    /// <param name="cancellation">Stops sending heartbeats.</param>
    public void Maintain(IPeerTransport transport, UserEndpoint target, CancellationToken cancellation)
        => _ = Task.Run(() => Loop(transport, target, cancellation), cancellation);

    private async Task Loop(IPeerTransport transport, UserEndpoint target, CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            bool connected;
            try
            {
                connected = await transport.Request(target, ReadOnlyMemory<byte>.Empty, new PeerSendOptions { Priority = int.MinValue }, cancellation);
            }
            catch
            {
                connected = false;
            }

            try { await Task.Delay(connected ? steadyInterval : fastRetryInterval, cancellation); }
            catch (OperationCanceledException) { return; }
        }
    }
}
