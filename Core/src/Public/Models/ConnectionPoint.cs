namespace BlueHeighliner.Comlink.Models;

/// <summary>
/// A place this node connects out to: either an IP host and TCP port, or a MicroGate serial port name and HDLC
/// address. Setting <see cref="SerialPort"/> makes it a serial point and the IP members are ignored. A point says
/// nothing about who is at the other end; that is worked out when the connection forms.
/// </summary>
public sealed class ConnectionPoint : IEquatable<ConnectionPoint>
{
    /// <summary>IP address of the remote node. Unused for a serial point.</summary>
    public string IpAddress { get; init; } = string.Empty;
    /// <summary>TCP port of the remote node. Unused for a serial point.</summary>
    public int Port { get; init; }
    /// <summary>Name of the local MicroGate serial port the remote node is cabled to, or <see langword="null"/> for an IP point.</summary>
    public string? SerialPort { get; init; }
    /// <summary>HDLC station address used on the serial link. Both ends of a cable must use the same value.</summary>
    public byte SerialAddress { get; init; } = 0xFF;

    /// <summary>Whether this point is reached over a MicroGate serial port rather than IP.</summary>
    public bool IsSerial => !string.IsNullOrEmpty(SerialPort);

    /// <summary>A stable identity for this point, equal for two points that reach the same place.</summary>
    public string Key => IsSerial
        ? $"serial:{SerialPort!.ToUpperInvariant()}:{SerialAddress}"
        : $"ip:{IpAddress.ToUpperInvariant()}:{Port}";

    /// <inheritdoc />
    public bool Equals(ConnectionPoint? other) => other is not null && Key == other.Key;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ConnectionPoint);

    /// <inheritdoc />
    public override int GetHashCode() => Key.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => IsSerial ? $"{SerialPort} (address {SerialAddress})" : $"{IpAddress}:{Port}";
}
