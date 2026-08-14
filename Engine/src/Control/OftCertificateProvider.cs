namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>
/// Provides TLS certificates and validation callbacks for OFT peer connections.
/// Register a custom implementation to use CA-signed or pinned certificates instead of the default self-signed cert.
/// </summary>
public interface IOftCertificateProvider
{
    /// <summary>Returns the peer options — including TLS certificate, validation, and security mode — used for both inbound and outbound OFT peer connections.</summary>
    OftPeerOptions GetPeerOptions();
}

/// <summary>
/// Implements <see cref="IOftCertificateProvider"/> locating certificates by name in the system certificate
/// store. <see cref="GetPeerOptions"/> is <see langword="virtual"/> so a host can inherit and override it —
/// see <c>Docs/Control.md</c> — though for most customization needs, overriding <see cref="IOftPeerCertificateName"/>
/// instead is sufficient and does not require touching this security-sensitive class at all.
/// </summary>
[ExcludeFromCodeCoverage]
public class DefaultOftCertificateProvider(
    IOftPeerCertificateName certNameProvider,
    ICurrentUserProvider currentUserProvider) : IOftCertificateProvider
{
    /// <inheritdoc />
    public virtual OftPeerOptions GetPeerOptions()
    {
        X509Certificate2? cert = GetOwnCertificate();
        return new OftPeerOptions
        {
            Info = currentUserProvider.UserName ?? string.Empty,
            Certificate = cert,
            CertificateValidation = cert is not null ? ValidateChain : null,
            SecurityMode = cert is not null ? OftSecurityMode.DualAuthentication : OftSecurityMode.Secure
        };
    }

    private X509Certificate2? GetOwnCertificate()
    {
        string? userName = currentUserProvider.UserName;
        if (userName is null) return null;
        string? certName = certNameProvider.GetCertificateName(userName);
        if (certName is null) return null;
        return FindCertificate(certName)
            ?? throw new InvalidOperationException(
                $"Peer authentication is required but no certificate named '{certName}' was found in the system store. " +
                "Install the certificate or set PeerCertificateName to \"disable\" to run without authentication.");
    }

    private static X509Certificate2? FindCertificate(string name)
    {
        foreach (StoreLocation location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            using X509Store store = new(StoreName.My, location);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                foreach (X509Certificate2 cert in store.Certificates)
                {
                    if (string.Equals(cert.GetNameInfo(X509NameType.SimpleName, false), name, StringComparison.OrdinalIgnoreCase))
                        return cert;
                }
            }
            catch { }
        }
        return null;
    }

    private static bool ValidateChain(object _, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors)
        => errors == SslPolicyErrors.None;
}
