namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>
/// Moves opaque messages between this node and remote nodes over IP or serial, hiding which one a connection uses.
/// A node connects out to <see cref="ConnectionPoint"/>s and listens for connections from others; either way the
/// result is a <see cref="PeerConnection"/>, and every send goes over one. A send completes only once the remote node
/// has acknowledged the message.
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

    /// <summary>
    /// Closes or reopens <paramref name="point"/>. While closed, the current connection to it is dropped, no new one is
    /// formed (a serial link stops trying to reconnect), and <see cref="Connect"/> to it fails immediately.
    /// </summary>
    void SetClosed(ConnectionPoint point, bool closed);

    /// <summary>Drops the current connection to <paramref name="point"/>, if any, so a new one forms on the next <see cref="Connect"/> (a serial link reconnects on its own). Has no effect while the point is closed.</summary>
    void Reset(ConnectionPoint point);

    /// <summary>Returns the connection to <paramref name="point"/>, opening it first when there is none.</summary>
    /// <exception cref="IOException">The connection could not be made, or was not accepted.</exception>
    Task<PeerConnection> Connect(ConnectionPoint point, CancellationToken cancellation = default);

    /// <summary>Sends <paramref name="data"/> over <paramref name="connection"/> and waits for the remote node to acknowledge it.</summary>
    /// <returns><see langword="true"/> if the remote node accepted the message, <see langword="false"/> if it rejected it.</returns>
    /// <exception cref="IOException">The message could not be delivered (the connection is gone, or dropped while waiting).</exception>
    Task<bool> Request(PeerConnection connection, ReadOnlyMemory<byte> data, PeerSendOptions? options = null, CancellationToken cancellation = default);
}
