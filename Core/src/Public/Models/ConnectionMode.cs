namespace BlueHeighliner.Comlink.Models;

/// <summary>How a node forms the connection of one link to its parent or one of its children. See <see cref="UserLink"/>.</summary>
public enum ConnectionMode
{
    /// <summary>The node listens for an incoming MSMT connection from the other user, on its own <see cref="PeerPoint"/>.</summary>
    MsmtListen,
    /// <summary>The node opens an outgoing MSMT connection to the other user, at that user's <see cref="PeerPoint"/>.</summary>
    MsmtConnect,
    /// <summary>The node forms a MicroGate peer connection over a serial port. Both ends of the cable name the port and the two station addresses.</summary>
    SyncSerial
}
