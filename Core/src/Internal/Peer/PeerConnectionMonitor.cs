namespace BlueHeighliner.Comlink;

/// <summary>
/// Proactively opens, and continuously maintains, the connection to one of this node's outgoing
/// <see cref="ConnectionPoint"/>s by connecting to it and, over IP only, requesting a heartbeat over the connection,
/// awaiting its outcome, but only when the host states a heartbeat handler (see <see cref="IEngineController.HeartbeatsEnabled"/> and <see cref="IEngineController.PacketHeartbeatsEnabled"/>, the latter taking precedence and sending the heartbeat as a packet beneath packetization); without one a connection counts as up once established. The heartbeat is a frame of the host's own heartbeat handler, serialized like any frame (see
/// <see cref="IEngineController.IsHeartbeat"/>), so nothing but serialized frames and packets ever crosses a connection;
/// <see cref="PeerFrameDispatcher.Dispatch"/> and <see cref="ServerRoutingService"/> acknowledge it without acting on it, so it never
/// reaches the remote peer's application logic. Over IP a heartbeat reuses the
/// same cached session connection, so the connection genuinely stays open between heartbeats rather than flapping; over
/// serial no heartbeat is ever sent: HDLC reports the state of its own link, so a serial point counts as up while its link is connected, and the first connect is what creates the link to the port, which then reconnects on its own.
/// </summary>
/// <remarks>
/// While the last heartbeat did not succeed - including the very first one, which commonly races the
/// remote peer's own receiver still starting up (a fresh <c>ECONNREFUSED</c> is near-instant, not a slow
/// timeout) - heartbeats retry on <see cref="FastRetryInterval"/> instead of <see cref="SteadyInterval"/>,
/// so a transient startup race resolves within a couple of seconds rather than sitting on a hierarchical
/// connection's status table as incorrectly "down" for up to a full <see cref="SteadyInterval"/>. The same
/// fast retry re-engages immediately after any later drop. The connection's live state is then observed the
/// normal way, through <see cref="IPeerTransport.Connected"/>/<see cref="IPeerTransport.Disconnected"/>, by
/// whichever caller subscribes to those observables. See <c>Docs/Components/Peer.md</c>.
/// </remarks>
internal sealed class PeerConnectionMonitor(IEngineController engineController, ILogger? logger = null, TimeSpan? steadyInterval = null, TimeSpan? fastRetryInterval = null)
{
    private TimeSpan SteadyInterval => steadyInterval ?? engineController.HeartbeatInterval;
    private TimeSpan FastRetryInterval => fastRetryInterval ?? engineController.HeartbeatRetryInterval;

    /// <summary>
    /// Starts connecting to <paramref name="target"/> through <paramref name="transport"/> and requesting a
    /// heartbeat over the connection in the background, immediately and then repeatedly - on <see cref="FastRetryInterval"/>
    /// while disconnected, on <see cref="SteadyInterval"/> once connected - until <paramref
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
        bool failureLogged = false;
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
                    if (target.IsSerial || !(engineController.HeartbeatsEnabled || engineController.PacketHeartbeatsEnabled))
                    {
                        connected = true;
                    }
                    else if (engineController.PacketHeartbeatsEnabled)
                    {
                        byte[] heartbeat;
                        object heartbeatPacket = engineController.CreatePacketHeartbeat();
                        try
                        {
                            using (IMemoryOwner<byte> owner = engineController.PacketSerializer!.Serialize(heartbeatPacket, null)) { heartbeat = owner.Memory.ToArray(); }
                        }
                        finally
                        {
                            heartbeatPacket.TryDispose();
                        }

                        connected = await transport.Request(connection, heartbeat, new PeerSendOptions { Priority = engineController.PacketHeartbeatPriority, IsPacket = true }, cancellation);
                    }
                    else
                    {
                        object frame = engineController.CreateHeartbeat();
                        try
                        {
                            byte[] heartbeat;
                            using (IMemoryOwner<byte> owner = engineController.FrameSerializer.Serialize(frame)) { heartbeat = owner.Memory.ToArray(); }
                            connected = await transport.Request(connection, heartbeat, new PeerSendOptions { Priority = engineController.HeartbeatPriority, Frame = frame }, cancellation);
                        }
                        finally
                        {
                            frame.TryDispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    connected = false;
                    if (!failureLogged && !cancellation.IsCancellationRequested)
                    {
                        failureLogged = true;
                        logger?.Record(LogEvents.PeerConnectFailed, "Cannot reach {Point}, retrying: {Reason}", target, ex.Message);
                    }
                }

                if (connected)
                {
                    failureLogged = false;
                }

                delay = connected ? SteadyInterval : FastRetryInterval;
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
