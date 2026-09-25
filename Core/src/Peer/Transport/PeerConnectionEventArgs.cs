namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>Published when a connection is established or lost.</summary>
internal sealed record PeerConnectionEventArgs
{
    /// <summary>The connection that came up or went down.</summary>
    public required PeerConnection Connection { get; init; }
}
