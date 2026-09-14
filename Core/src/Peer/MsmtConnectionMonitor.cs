namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// Proactively opens, and continuously maintains, a connection to each of a Client or Server role's
/// hierarchical peers (a Client's own server, or a Server's own children and sibling servers) by requesting
/// an empty heartbeat payload and awaiting its outcome - <see cref="PeerMessageDispatcher.Dispatch"/> and
/// <see cref="ServerRoutingService"/> both treat an empty payload as a no-op, so it never reaches the remote
/// peer's application logic. Unlike <see cref="IMsmtPeer.Test"/> - which opens and immediately closes a
/// dedicated one-shot connection, visibly cycling the remote peer's own <see cref="IMsmtPeer.Connected"/>/
/// <see cref="IMsmtPeer.Disconnected"/> observables every time - a heartbeat reuses the same cached, <see
/// cref="MsmtOperationMode.Session"/>-mode connection <see cref="IMsmtPeer.Request"/> already creates on
/// demand, so the connection genuinely stays open between heartbeats rather than flapping.
/// </summary>
/// <remarks>
/// While the last heartbeat did not succeed - including the very first one, which commonly races the
/// remote peer's own receiver still starting up (a fresh <c>ECONNREFUSED</c> is near-instant, not a slow
/// timeout) - heartbeats retry on <see cref="fastRetryInterval"/> instead of <see cref="steadyInterval"/>,
/// so a transient startup race resolves within a couple of seconds rather than sitting on a hierarchical
/// connection's status table as incorrectly "down" for up to a full <see cref="steadyInterval"/>. The same
/// fast retry re-engages immediately after any later drop, so a genuine mid-session disconnect is also
/// noticed and re-established quickly rather than only on the next slow tick. The connection's live state
/// is then observed the normal way, through <see cref="IMsmtPeer.Connected"/>/<see
/// cref="IMsmtPeer.Disconnected"/>, by whichever caller subscribes to those observables - this class only
/// keeps traffic flowing so a hierarchical connection reflects reality continuously instead of only at the
/// moment a real message happened to be sent. See <c>Docs/Components/Peer.md</c>.
/// </remarks>
internal sealed class MsmtConnectionMonitor(TimeSpan? steadyInterval = null, TimeSpan? fastRetryInterval = null)
{
    private readonly TimeSpan steadyInterval = steadyInterval ?? TimeSpan.FromSeconds(30);
    private readonly TimeSpan fastRetryInterval = fastRetryInterval ?? TimeSpan.FromSeconds(2);

    /// <summary>
    /// Starts requesting an empty heartbeat payload from <paramref name="target"/> through <paramref
    /// name="peer"/> in the background, immediately and then repeatedly - on <see cref="fastRetryInterval"/>
    /// while disconnected, on <see cref="steadyInterval"/> once connected - until <paramref
    /// name="cancellation"/> is triggered.
    /// </summary>
    /// <param name="peer">The peer to send heartbeats through.</param>
    /// <param name="target">The hierarchical peer to maintain a connection to.</param>
    /// <param name="cancellation">Stops sending heartbeats.</param>
    public void Maintain(IMsmtPeer peer, MsmtNameTarget target, CancellationToken cancellation)
        => _ = Task.Run(() => Loop(peer, target, cancellation), cancellation);

    private async Task Loop(IMsmtPeer peer, MsmtNameTarget target, CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            bool connected;
            try
            {
                MsmtResponse response = await peer.Request(target, ReadOnlyMemory<byte>.Empty, new MsmtSendOptions { Priority = int.MinValue }, cancellation);
                response.Payload.Dispose();
                connected = response.Success;
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
