namespace BlueHeighliner.Comlink;

/// <summary>A connection over a MicroGate serial port: the port and the station addresses of both ends.</summary>
public interface ISerialConnectionInfo : IConnectionInfo
{
    /// <summary>Gets the HDLC station address of this node on the serial link.</summary>
    byte SerialAddress { get; }

    /// <summary>Gets the HDLC station address of the node at the other end of the serial link.</summary>
    byte RemoteSerialAddress { get; }

    /// <summary>Gets the name of the local serial port the connection runs over.</summary>
    string SerialPort { get; }
}
