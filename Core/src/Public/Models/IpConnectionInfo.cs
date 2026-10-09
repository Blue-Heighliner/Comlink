namespace BlueHeighliner.Comlink;

/// <summary>A connection over IP: the remote host, port and the names in its certificate.</summary>
public interface IIpConnectionInfo : IConnectionInfo
{
    /// <summary>Gets the remote TCP port.</summary>
    int Port { get; }

    /// <summary>Gets the remote IP address. For an inbound connection the port is the remote node's ephemeral one, so identify by certificate or handshake packet or message instead.</summary>
    string Host { get; }

    /// <summary>Gets the distinguished name of the remote certificate, or <see langword="null"/> when it presented none.</summary>
    string? CertificateSubject { get; }

    /// <summary>Gets the common names (<c>CN=</c> components) of the remote certificate's subject.</summary>
    IReadOnlyList<string> CertificateNames { get; }
}
