namespace BlueHeighliner.Comlink;

/// <summary>The implementation behind <see cref="IIpConnectionInfo"/>.</summary>
internal sealed record IpConnectionInfo : ConnectionInfo, IIpConnectionInfo
{
    /// <inheritdoc />
    public string Host { get; init; } = string.Empty;

    /// <inheritdoc />
    public int Port { get; init; }

    /// <inheritdoc />
    public string? CertificateSubject { get; init; }

    /// <inheritdoc />
    public IReadOnlyList<string> CertificateNames { get; init; } = [];
}
