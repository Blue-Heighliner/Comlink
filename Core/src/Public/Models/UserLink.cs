namespace BlueHeighliner.Comlink.Models;

/// <summary>
/// One connection between a user and its parent or one of its children. By default a user opens an outgoing connection to its parent and listens for incoming
/// connections from its children, so naming the other user is enough; stating a <see cref="Mode"/> forces the link into a specific mode, and the other end of the link
/// states the matching one (a child forced to <see cref="ConnectionMode.MsmtConnect"/> is dialed by its parent, so the child states <see cref="ConnectionMode.MsmtListen"/> for its parent).
/// </summary>
public sealed record UserLink
{
    /// <summary>Reads a plain user name as a link with the default mode.</summary>
    /// <param name="user">The name of the user at the other end.</param>
    public static implicit operator UserLink(string user) => new() { User = user };

    /// <summary>Gets the name of the user at the other end of the link.</summary>
    public required string User { get; init; }

    /// <summary>Gets the forced connection mode, or <see langword="null"/> for the default: <see cref="ConnectionMode.MsmtConnect"/> to a parent, <see cref="ConnectionMode.MsmtListen"/> for a child, and <see cref="ConnectionMode.SyncSerial"/> when a <see cref="SerialPort"/> is named.</summary>
    public ConnectionMode? Mode { get; init; }

    /// <summary>Gets, for <see cref="ConnectionMode.SyncSerial"/>, the name of the local MicroGate serial port cabled to the other user.</summary>
    public string? SerialPort { get; init; }

    /// <summary>Gets, for <see cref="ConnectionMode.SyncSerial"/>, this node's HDLC station address on the serial link (0-255). Must differ from <see cref="RemoteSerialAddress"/>. The default is 255.</summary>
    public byte SerialAddress { get; init; } = 0xFF;

    /// <summary>Gets, for <see cref="ConnectionMode.SyncSerial"/>, the HDLC station address of the other user's end of the cable (0-255). The other end states the two addresses the other way round. The default is 254.</summary>
    public byte RemoteSerialAddress { get; init; } = 0xFE;
}
