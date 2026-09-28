namespace BlueHeighliner.Comlink.Models;

/// <summary>
/// What is known about a connection that has just formed, handed to <see cref="IEngineController.IdentifyConnection"/>
/// and the connection message hooks so a host can decide who is on the other end. An IP connection describes the
/// remote host, port and the names in its certificate; a serial connection describes the port and station address it
/// runs over. When a connection message is configured, <see cref="ConnectionMessage"/> and
/// <see cref="ConnectionResponse"/> carry what the remote node sent during the exchange.
/// </summary>
public sealed record ConnectionInfo
{
    /// <summary>Gets a value indicating whether the connection runs over a MicroGate serial port rather than IP.</summary>
    public bool IsSerial { get; init; }

    /// <summary>Gets a value indicating whether the remote node opened the connection to this node's listener. A serial connection is never inbound, since both ends open the port.</summary>
    public bool IsInbound { get; init; }

    /// <summary>Gets the remote IP address. For an inbound connection the port is the remote node's ephemeral one, so identify by certificate or connection message instead. Empty for serial.</summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>Gets the remote TCP port. Unused for serial.</summary>
    public int Port { get; init; }

    /// <summary>Gets the distinguished name of the remote certificate, or <see langword="null"/> for serial.</summary>
    public string? CertificateSubject { get; init; }

    /// <summary>Gets the common names (<c>CN=</c> components) of the remote certificate's subject. Empty for serial.</summary>
    public IReadOnlyList<string> CertificateNames { get; init; } = [];

    /// <summary>Gets the name of the local serial port the connection runs over, or <see langword="null"/> for IP.</summary>
    public string? SerialPort { get; init; }

    /// <summary>Gets the HDLC station address of the serial link. Unused for IP.</summary>
    public byte SerialAddress { get; init; }

    /// <summary>Gets the connection message the remote node sent, or <see langword="null"/> when none was configured, none was sent, or it has not arrived.</summary>
    public object? ConnectionMessage { get; init; }

    /// <summary>Gets the connection response the remote node sent, or <see langword="null"/> when none was configured, none was sent, or it has not arrived.</summary>
    public object? ConnectionResponse { get; init; }
}
