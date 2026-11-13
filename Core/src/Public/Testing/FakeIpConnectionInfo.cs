namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="IIpConnectionInfo"/> of an IP connection, to give a fake handshake context or a handler under test a connection of that kind. Every property is set as the test needs it.</summary>
public sealed class FakeIpConnectionInfo : IIpConnectionInfo
{
    /// <inheritdoc />
    public bool IsInbound { get; set; }

    /// <inheritdoc />
    public string? LocalUser { get; set; }

    /// <inheritdoc />
    public string Host { get; set; } = "127.0.0.1";

    /// <inheritdoc />
    public int Port { get; set; }

    /// <inheritdoc />
    public string? CertificateSubject { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<string> CertificateNames { get; set; } = [];
}
