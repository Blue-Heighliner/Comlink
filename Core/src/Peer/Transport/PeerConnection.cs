namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>One live connection to a remote node, over IP or serial, as seen by the peer services.</summary>
internal sealed class PeerConnection(UserEndpoint? endpoint, bool isInbound, string? identitySubject, Action drop)
{
    /// <summary>The endpoint this node dialed or cabled to, or <see langword="null"/> for a connection a remote node opened to this one.</summary>
    public UserEndpoint? Endpoint { get; } = endpoint;

    /// <summary>Whether a remote node opened this connection to this node's listener. A serial link is never inbound: this node opens the port itself.</summary>
    public bool IsInbound { get; } = isInbound;

    /// <summary>The remote certificate's distinguished name for an IP connection, or <see langword="null"/> when there is none (serial).</summary>
    public string? IdentitySubject { get; } = identitySubject;

    /// <summary>Forcibly closes the connection.</summary>
    public void Drop() => drop();
}
