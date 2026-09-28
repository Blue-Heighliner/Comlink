namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>One live connection to a remote node, over IP or serial, as seen by the peer services.</summary>
internal sealed class PeerConnection(ConnectionPoint? point, ConnectionInfo info, Action drop)
{
    private readonly string inboundKey = $"in:{Guid.NewGuid():N}";

    /// <summary>The point this node dialed or cabled to, or <see langword="null"/> for a connection a remote node opened to this one.</summary>
    public ConnectionPoint? Point { get; } = point;

    /// <summary>What is known about the connection. Replaced as the connection message exchange delivers more.</summary>
    public ConnectionInfo Info { get; set; } = info;

    /// <summary>Who is on the other end, or <see langword="null"/> until the connection has been identified.</summary>
    public UserIdentity? User { get; set; }

    /// <summary>Whether a remote node opened this connection to this node's listener. A serial link is never inbound: this node opens the port itself.</summary>
    public bool IsInbound => Info.IsInbound;

    /// <summary>Whether the connection runs over a serial port.</summary>
    public bool IsSerial => Info.IsSerial;

    /// <summary>A stable identity for the connection: its point's key for one this node opened, otherwise unique to the connection.</summary>
    public string Key => Point?.Key ?? inboundKey;

    /// <summary>Forcibly closes the connection.</summary>
    public void Drop() => drop();
}
