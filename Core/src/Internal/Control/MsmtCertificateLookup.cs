namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// Shared certificate store lookup for <see cref="EngineController.ConnectionOptions"/> and
/// <see cref="ConfiguredEngineController.ConnectionOptions"/>.
/// </summary>
internal static class MsmtCertificateLookup
{
    /// <summary>
    /// Looks up <paramref name="currentUserName"/>'s identity certificate via <paramref
    /// name="getCertificateName"/> and <paramref name="trustedAuthorityCertificateName"/>'s certificate
    /// authority (each caller passes its own, potentially config-overridden, values) in the system
    /// certificate store.
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="currentUserName"/> is <see langword="null"/>, so no identity certificate can be resolved.</exception>
    public static MsmtSessionPeerOptions BuildPeerOptions(string? currentUserName, Func<string, string> getCertificateName, string trustedAuthorityCertificateName)
    {
        if (currentUserName is null)
        {
            throw new InvalidOperationException("Peer connection options require a registered current user to resolve an identity certificate for.");
        }

        string certName = getCertificateName(currentUserName);
        X509Certificate2 identity = FindCertificate(certName)
            ?? throw new InvalidOperationException($"Peer authentication requires a certificate named '{certName}', but none was found in the system store. Install the certificate to continue.");
        X509Certificate2 authority = FindCertificate(trustedAuthorityCertificateName)
            ?? throw new InvalidOperationException($"Peer authentication requires a trusted authority certificate named '{trustedAuthorityCertificateName}', but none was found in the system store. Install the certificate to continue.");

        return new MsmtSessionPeerOptions
        {
            Credentials = new MsmtCredentials { Identity = identity, TrustedAuthorities = [authority] },
            RequireFullyQualifiedHostname = false
        };
    }

    /// <summary>
    /// Builds peer options directly from certificate files on disk instead of a system store lookup - used
    /// when the network configuration file's <c>CertificateFile</c>/<c>TrustedAuthorityCertificateFile</c> fields
    /// are set. <paramref name="peerCertificateFile"/> must be a PKCS#12 file carrying the identity
    /// certificate's private key; <paramref name="trustedAuthorityCertificateFile"/> a public certificate
    /// file for the trusted authority.
    /// </summary>
    /// <exception cref="InvalidOperationException">Either file does not exist.</exception>
    public static MsmtSessionPeerOptions BuildPeerOptionsFromFiles(string peerCertificateFile, string trustedAuthorityCertificateFile)
    {
        if (!File.Exists(peerCertificateFile))
        {
            throw new InvalidOperationException($"Peer authentication requires an identity certificate file at '{peerCertificateFile}', but it was not found.");
        }
        if (!File.Exists(trustedAuthorityCertificateFile))
        {
            throw new InvalidOperationException($"Peer authentication requires a trusted authority certificate file at '{trustedAuthorityCertificateFile}', but it was not found.");
        }

        X509Certificate2 identity = X509CertificateLoader.LoadPkcs12FromFile(peerCertificateFile, password: null);
        X509Certificate2 authority = X509CertificateLoader.LoadCertificateFromFile(trustedAuthorityCertificateFile);

        return new MsmtSessionPeerOptions
        {
            Credentials = new MsmtCredentials { Identity = identity, TrustedAuthorities = [authority] },
            RequireFullyQualifiedHostname = false
        };
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
                    {
                        return cert;
                    }
                }
            }
            catch { }
        }
        return null;
    }
}
