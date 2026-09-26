namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>
/// Moves opaque messages between this node and remote nodes over IP or serial, hiding which one an endpoint uses.
/// A send completes only once the remote node has acknowledged the message.
/// </summary>
internal interface IPeerTransport : IAsyncDisposable
{
    /// <summary>Publishes every message received on any connection.</summary>
    IObservable<PeerReceivedEventArgs> Received { get; }
    /// <summary>Publishes when a connection is established.</summary>
    IObservable<PeerConnectionEventArgs> Connected { get; }
    /// <summary>Publishes when a connection is lost.</summary>
    IObservable<PeerConnectionEventArgs> Disconnected { get; }

    /// <summary>Starts accepting IP connections on <paramref name="port"/>. Has no effect when IP is unavailable, and serial links need no listener.</summary>
    void StartListener(int port);

    /// <summary>Makes sure the link to <paramref name="endpoint"/> is being established, so messages from that side are received even before anything is sent to it. IP connections are opened on demand, so this only matters for serial.</summary>
    void Open(UserEndpoint endpoint);

    /// <summary>
    /// Closes or reopens <paramref name="endpoint"/>. While closed, the current connection to it is dropped, no new one is
    /// formed (a serial link stops trying to reconnect), and a <see cref="Request"/> to it fails immediately.
    /// </summary>
    void SetClosed(UserEndpoint endpoint, bool closed);

    /// <summary>Drops the current connection to <paramref name="endpoint"/>, if any, so a new one forms on the next request (a serial link reconnects on its own). Has no effect while the endpoint is closed.</summary>
    void Reset(UserEndpoint endpoint);

    /// <summary>Sends <paramref name="data"/> to <paramref name="target"/> and waits for the remote node to acknowledge it.</summary>
    /// <returns><see langword="true"/> if the remote node accepted the message, <see langword="false"/> if it rejected it.</returns>
    /// <exception cref="IOException">The message could not be delivered (no connection, or the link dropped).</exception>
    Task<bool> Request(UserEndpoint target, ReadOnlyMemory<byte> data, PeerSendOptions? options = null, CancellationToken cancellation = default);
}
