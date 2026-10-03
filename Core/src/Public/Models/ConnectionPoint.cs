namespace BlueHeighliner.Comlink;

/// <summary>
/// A place this node connects out to: either an IP host and TCP port, or a MicroGate serial port name and HDLC
/// address. Setting <see cref="SerialPort"/> makes it a serial point and the IP members are ignored. A point says
/// nothing about who is at the other end; that is worked out when the connection forms, except that a serial point, which
/// carries no certificate, may name the user at the other end of its cable with <see cref="User"/>.
/// </summary>
public sealed class ConnectionPoint : IEquatable<ConnectionPoint>
{
    /// <summary>IP address of the remote node. Unused for a serial point.</summary>
    public string IpAddress { get; init; } = string.Empty;
    /// <summary>TCP port of the remote node. Unused for a serial point.</summary>
    public int Port { get; init; }
    /// <summary>Name of the local MicroGate serial port the remote node is cabled to, or <see langword="null"/> for an IP point.</summary>
    public string? SerialPort { get; init; }
    /// <summary>HDLC station address of this node on the serial link. It must differ from <see cref="RemoteSerialAddress"/>, and the node at the other end of the cable must use the two values the other way round.</summary>
    public byte SerialAddress { get; init; } = 0xFF;
    /// <summary>HDLC station address of the node at the other end of the serial link. It must differ from <see cref="SerialAddress"/>, and be the other end's own <see cref="SerialAddress"/>.</summary>
    public byte RemoteSerialAddress { get; init; } = 0xFE;

    /// <summary>
    /// For a serial point, the user at the other end of the cable, which a serial connection (with no certificate to identify it by)
    /// is identified as. <see langword="null"/> (the default) names the user after the port. Unused for an IP point.
    /// </summary>
    public string? User { get; init; }

    /// <summary>
    /// For a serial point reached by a node with several HDLC links, the other users it may be cabled to besides <see cref="User"/> at <see cref="RemoteSerialAddress"/>: a link tries each in turn
    /// until the one at the other end of the cable answers. Empty (the default) for a point with a single known remote. Unused for an IP point.
    /// </summary>
    public IReadOnlyList<HdlcRemote> OtherRemotes { get; init; } = [];

    /// <summary>Whether this point is reached over a MicroGate serial port rather than IP.</summary>
    public bool IsSerial => !string.IsNullOrEmpty(SerialPort);

    /// <summary>A stable identity for this point, equal for two points that reach the same place.</summary>
    public string Key => IsSerial
        ? $"serial:{SerialPort!.ToUpperInvariant()}:{SerialAddress}"
        : $"ip:{IpAddress.ToUpperInvariant()}:{Port}";

    /// <summary>Returns the user known to be at HDLC station address <paramref name="remoteAddress"/> at the other end of this serial point, or <see langword="null"/> when none is.</summary>
    /// <param name="remoteAddress">The remote station address a connection formed with.</param>
    public string? UserAt(byte remoteAddress)
        => remoteAddress == RemoteSerialAddress ? User : OtherRemotes.FirstOrDefault(remote => remote.Address == remoteAddress)?.User;

    /// <inheritdoc />
    public bool Equals(ConnectionPoint? other) => other is not null && Key == other.Key;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ConnectionPoint);

    /// <inheritdoc />
    public override int GetHashCode() => Key.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => IsSerial ? $"{SerialPort} (address {SerialAddress})" : $"{IpAddress}:{Port}";
}
