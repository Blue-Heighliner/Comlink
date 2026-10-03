namespace BlueHeighliner.Comlink.Models;

/// <summary>How a node forms the connection of one link to its parent or one of its children. See <see cref="UserLink"/>.</summary>
public enum ConnectionMode
{
    /// <summary>The node listens for an incoming MSMT connection from the other user, on its own <see cref="UserInfo.MsmtPort"/>.</summary>
    MsmtListen,
    /// <summary>The node opens an outgoing MSMT connection to the other user, at that user's <see cref="UserInfo.IpHost"/> and <see cref="UserInfo.MsmtPort"/>.</summary>
    MsmtConnect,
    /// <summary>The node forms an HDLC peer connection over its HDLC ports (see <see cref="UserInfo.HdlcPorts"/>), using its own and the other user's <see cref="UserInfo.HdlcAddress"/>.</summary>
    Hdlc
}
