namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>
/// Determines the certificate subject name used to locate TLS certificates
/// in the system store for authenticated OFT peer connections.
/// </summary>
public interface IOftPeerCertificateName
{
    /// <summary>
    /// Returns the certificate subject name to search for in the system store for the given site.
    /// Returns <see langword="null"/> to disable peer authentication (unauthenticated mode).
    /// When a non-null name is returned and no matching certificate exists, startup throws.
    /// </summary>
    /// <param name="siteName">The local site name for which to resolve a certificate name.</param>
    string? GetCertificateName(string siteName);
}

/// <summary>
/// Implements <see cref="IOftPeerCertificateName"/> using the certificate name from engine configuration.
/// <c>null</c> config value → auto (<c>SITE-{siteName}</c>); <c>"disable"</c> → no auth; explicit name → use that name.
/// </summary>
internal sealed class OftPeerCertificateName : IOftPeerCertificateName
{
    private readonly EngineConfig _config;

    /// <summary>Initializes a new instance with the given engine configuration.</summary>
    /// <param name="config">Engine configuration providing the certificate name setting.</param>
    public OftPeerCertificateName(EngineConfig config) => _config = config;

    /// <inheritdoc />
    public string? GetCertificateName(string siteName) =>
        _config.PeerCertificateName switch
        {
            null => $"SITE-{siteName}",
            "disable" => null,
            string name => name
        };
}
