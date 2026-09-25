namespace BlueHeighliner.Comlink.Models;

/// <summary>
/// Where to reach a remote user: either an IP host and TCP port, or a MicroGate serial port name and HDLC
/// address. Setting <see cref="SerialPort"/> makes it a serial endpoint and the IP members are ignored.
/// </summary>
public sealed class UserEndpoint : IEquatable<UserEndpoint>
{
    /// <summary>IP address of the remote user. Unused for a serial endpoint.</summary>
    public string IpAddress { get; init; } = string.Empty;
    /// <summary>TCP port of the remote user. Unused for a serial endpoint.</summary>
    public int Port { get; init; }
    /// <summary>Name of the local MicroGate serial port the remote user is cabled to, or <see langword="null"/> for an IP endpoint.</summary>
    public string? SerialPort { get; init; }
    /// <summary>HDLC station address used on the serial link. Both ends of a cable must use the same value.</summary>
    public byte SerialAddress { get; init; } = 0xFF;

    /// <summary>Whether this endpoint is reached over a MicroGate serial port rather than IP.</summary>
    public bool IsSerial => !string.IsNullOrEmpty(SerialPort);

    /// <summary>A stable identity for this endpoint, equal for two endpoints that reach the same place.</summary>
    public string Key => IsSerial
        ? $"serial:{SerialPort!.ToUpperInvariant()}:{SerialAddress}"
        : $"ip:{IpAddress.ToUpperInvariant()}:{Port}";

    /// <inheritdoc />
    public bool Equals(UserEndpoint? other) => other is not null && Key == other.Key;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as UserEndpoint);

    /// <inheritdoc />
    public override int GetHashCode() => Key.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => IsSerial ? $"{SerialPort} (address {SerialAddress})" : $"{IpAddress}:{Port}";
}
