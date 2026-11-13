namespace BlueHeighliner.Comlink;

/// <summary>A connection over a MicroGate serial port: the port and the station addresses of both ends.</summary>
public interface ISerialConnectionInfo : IConnectionInfo
{
    /// <summary>Gets the HDLC station address of this node on the serial link.</summary>
    byte Address { get; }

    /// <summary>Gets the HDLC station address of the node at the other end of the serial link.</summary>
    byte RemoteAddress { get; }

    /// <summary>Gets the name of the local serial port the connection runs over.</summary>
    string Port { get; }
}
