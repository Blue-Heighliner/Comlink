namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IOftPeerCertificateName"/> that honors an explicit <c>config.json</c>
/// <c>PeerCertificateName</c> exactly like the Engine default, but in auto mode (config value <see langword="null"/>)
/// additionally checks a <c>CERT_NAME_{USERNAME}</c> environment variable before falling back to
/// <c>USER-{userName}</c>.
/// </summary>
public sealed class SampleOftPeerCertificateName : IOftPeerCertificateName
{
    private readonly EngineConfig _config;

    /// <summary>Initializes a new <see cref="SampleOftPeerCertificateName"/> with the given engine configuration.</summary>
    /// <param name="config">Engine configuration providing the certificate name setting.</param>
    public SampleOftPeerCertificateName(EngineConfig config) => _config = config;

    /// <inheritdoc />
    public string? GetCertificateName(string userName) =>
        _config.PeerCertificateName switch
        {
            null => Environment.GetEnvironmentVariable($"CERT_NAME_{userName.ToUpperInvariant().Replace("-", "_")}")
                ?? $"USER-{userName}",
            "disable" => null,
            string name => name
        };
}
