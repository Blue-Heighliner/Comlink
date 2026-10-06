namespace BlueHeighliner.Comlink;

/// <summary>
/// Builds the MSMT credentials of <see cref="ConfiguredEngineController.ConnectionOptions"/> from the certificate files the network configuration file designates.
/// </summary>
internal static class MsmtCertificateLookup
{
    /// <summary>
    /// Builds peer options from certificate files on disk, the ones the network configuration file's <c>CertificateStore</c>/<c>AuthorityCertificate</c> keys
    /// designate. <paramref name="peerCertificateFile"/> must be a PKCS#12 file carrying the identity
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
}
