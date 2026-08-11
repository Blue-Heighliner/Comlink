namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>
/// Determines the certificate subject name used to locate TLS certificates
/// in the system store for authenticated OFT peer connections.
/// </summary>
public interface IOftPeerCertificateName
{
    /// <summary>
    /// Returns the certificate subject name to search for in the system store for the given user.
    /// Returns <see langword="null"/> to disable peer authentication (unauthenticated mode).
    /// When a non-null name is returned and no matching certificate exists, startup throws.
    /// </summary>
    /// <param name="userName">The local user name for which to resolve a certificate name.</param>
    string? GetCertificateName(string userName);
}

/// <summary>
/// Implements <see cref="IOftPeerCertificateName"/> using the certificate name from engine configuration.
/// <c>null</c> config value → auto (<c>USER-{userName}</c>); <c>"disable"</c> → no auth; explicit name → use that name.
/// </summary>
internal sealed class OftPeerCertificateName : IOftPeerCertificateName
{
    private readonly EngineConfig _config;

    /// <summary>Initializes a new instance with the given engine configuration.</summary>
    /// <param name="config">Engine configuration providing the certificate name setting.</param>
    public OftPeerCertificateName(EngineConfig config) => _config = config;

    /// <inheritdoc />
    public string? GetCertificateName(string userName) =>
        _config.PeerCertificateName switch
        {
            null => $"USER-{userName}",
            "disable" => null,
            string name => name
        };
}
