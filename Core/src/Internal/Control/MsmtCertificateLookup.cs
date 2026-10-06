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

    /// <summary>
    /// Checks the identity certificate file of <paramref name="userName"/> against the authority certificate file: both must be set and exist, the identity must be readable
    /// and carry <paramref name="userName"/> as a subject common name, and it must chain to the authority certificate, the only root trusted.
    /// </summary>
    /// <param name="identityFile">The path of the user's PKCS#12 file, or <see langword="null"/> when the network has no certificate store.</param>
    /// <param name="authorityFile">The path of the authority certificate file, or <see langword="null"/> when the network has none.</param>
    /// <param name="userName">The user the identity must belong to.</param>
    /// <returns>Why the certificates are not good, or <see langword="null"/> when they are.</returns>
    public static string? GetProblem(string? identityFile, string? authorityFile, string userName)
    {
        if (identityFile is null || authorityFile is null) { return "The network's CertificateStore and AuthorityCertificate must both be set."; }
        if (!File.Exists(identityFile)) { return $"The certificate file '{identityFile}' for {userName} was not found."; }
        if (!File.Exists(authorityFile)) { return $"The authority certificate file '{authorityFile}' was not found."; }

        try
        {
            using X509Certificate2 identity = X509CertificateLoader.LoadPkcs12FromFile(identityFile, password: null);
            using X509Certificate2 authority = X509CertificateLoader.LoadCertificateFromFile(authorityFile);
            if (!PeerIdentity.ExtractCommonNames(identity.Subject).Contains(userName, StringComparer.OrdinalIgnoreCase))
            {
                return $"The certificate in '{identityFile}' is not issued to {userName}.";
            }

            using X509Chain chain = new();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(authority);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(identity) ? null : $"The certificate for {userName} is not signed by the authority certificate: {string.Join("; ", chain.ChainStatus.Select(status => status.StatusInformation.Trim()))}";
        }
        catch (Exception ex) when (ex is CryptographicException or IOException)
        {
            return $"A certificate file could not be read: {ex.Message}";
        }
    }
}
