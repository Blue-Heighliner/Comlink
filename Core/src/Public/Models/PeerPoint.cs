namespace BlueHeighliner.Comlink.Models;

/// <summary>Where a node can be reached over IP: the host and port other nodes connect to, and the port the node listens on.</summary>
public sealed record PeerPoint
{
    /// <summary>Gets the host or IP address other nodes use to reach this node. The default is the loopback address.</summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>Gets the TCP port this node listens on for IP connections and that other nodes connect to. The default is 50021.</summary>
    public int Port { get; init; } = 50021;
}
