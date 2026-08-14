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
/// Implements <see cref="IOftPeerCertificateName"/> with the auto-detected name <c>USER-{userName}</c>.
/// Describes non-config-file behavior; see <see cref="ConfiguredOftPeerCertificateName"/> for how
/// <c>config.json</c> overrides this. Members are <see langword="virtual"/> so a host can inherit and
/// override — see <c>Docs/Control.md</c>.
/// </summary>
public class DefaultOftPeerCertificateName : IOftPeerCertificateName
{
    /// <inheritdoc />
    public virtual string? GetCertificateName(string userName) => $"USER-{userName}";
}

/// <summary>
/// Engine-level decorator applying <see cref="EngineConfig.PeerCertificateName"/> over whichever
/// <see cref="IOftPeerCertificateName"/> is registered (Engine default or a host override):
/// <c>null</c> config value falls back to it; <c>"disable"</c> forces no authentication; an explicit
/// name is used as-is. Registered by <see cref="EngineExtensions.UseEngineConfigOverrides"/>, not by
/// control-interface convention scanning.
/// </summary>
internal sealed class ConfiguredOftPeerCertificateName : IOftPeerCertificateName
{
    private readonly IOftPeerCertificateName _fallback;
    private readonly EngineConfig _config;

    /// <summary>Initializes a new instance wrapping <paramref name="fallback"/> with config overrides.</summary>
    /// <param name="fallback">The registered control-interface implementation to fall back to when config does not override.</param>
    /// <param name="config">Engine configuration providing the certificate name setting.</param>
    public ConfiguredOftPeerCertificateName(IOftPeerCertificateName fallback, EngineConfig config)
    {
        _fallback = fallback;
        _config = config;
    }

    /// <inheritdoc />
    public string? GetCertificateName(string userName) =>
        _config.PeerCertificateName switch
        {
            null => _fallback.GetCertificateName(userName),
            "disable" => null,
            string name => name
        };
}
