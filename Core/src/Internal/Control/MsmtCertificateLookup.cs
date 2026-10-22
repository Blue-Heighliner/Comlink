namespace BlueHeighliner.Comlink;

/// <summary>
/// Works out who this node is from its certificate files, and builds the MSMT credentials of <see cref="ConfiguredEngineController.ConnectionOptions"/> from them.
/// </summary>
internal static class MsmtCertificateLookup
{
    /// <summary>
    /// Builds peer options from the certificate files on disk, the ones the host's <c>Certificates</c> call or the <c>--authority</c> and <c>--certificate</c> arguments designate. <paramref name="peerCertificateFile"/> must be a PKCS#12 file carrying the identity
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

        X509Certificate2 identity = X509CertificateLoader.LoadPkcs12FromFile(peerCertificateFile, password: null, keyStorageFlags: X509KeyStorageFlags.Exportable);
        X509Certificate2 authority = X509CertificateLoader.LoadCertificateFromFile(trustedAuthorityCertificateFile);

        return new MsmtSessionPeerOptions
        {
            Credentials = new MsmtCredentials { Identity = identity, TrustedAuthorities = [authority] },
            RequireFullyQualifiedHostname = false
        };
    }

    /// <summary>
    /// Checks the node's certificates and says who it is, or why that cannot be told. Never throws for a problem with the certificates: the problem is in the result, since a node that cannot be identified shows it to the user.
    /// </summary>
    /// <param name="builder">What the host stated, which says who a certificate means.</param>
    /// <param name="network">The users of the network and the command-line arguments.</param>
    public static IdentityCheck CheckIdentity(EngineBuilder builder, NetworkConfig network)
    {
        try
        {
            return new IdentityCheck { Identity = ResolveIdentity(builder, network) };
        }
        catch (InvalidOperationException ex)
        {
            (string? authority, string? certificate) = FilesOf(builder, network);
            return new IdentityCheck
            {
                Identity = new NodeIdentity { UserName = string.Empty, AuthorityFile = authority ?? string.Empty, CertificateFile = certificate ?? string.Empty, CertificateThumbprint = string.Empty, AuthorityThumbprint = string.Empty },
                Problem = ex.Message
            };
        }
    }

    /// <summary>
    /// Works out who this node is and checks the certificates it proves it with. The files are the command-line arguments when overrides are allowed, otherwise the host's <c>Certificates</c> call (a relative path is relative to
    /// the app data folder); the user certificate must be readable, chain to the authority certificate, the only root trusted, and identify, through the configuration handler's <see cref="IConfigurationHandler.Identify"/> (by default
    /// its first subject common name), a user the network lists, which is the user the process runs as.
    /// </summary>
    /// <param name="builder">What the host stated.</param>
    /// <param name="network">The users of the network and the command-line arguments.</param>
    /// <exception cref="InvalidOperationException">A file is not stated or does not exist, the certificate does not chain to the authority, or it identifies no user of the network.</exception>
    public static NodeIdentity ResolveIdentity(EngineBuilder builder, NetworkConfig network)
    {
        (string? authority, string? certificate) = FilesOf(builder, network);
        string authorityFile = authority ?? throw new InvalidOperationException("The node has no authority certificate: state one with Certificates(...) or pass --authority.");
        string certificateFile = certificate ?? throw new InvalidOperationException("The node has no user certificate: state one with Certificates(...) or pass --certificate.");

        if (!File.Exists(certificateFile))
        {
            throw new InvalidOperationException($"The user certificate file '{certificateFile}' was not found.");
        }
        if (!File.Exists(authorityFile))
        {
            throw new InvalidOperationException($"The authority certificate file '{authorityFile}' was not found.");
        }

        try
        {
            using X509Certificate2 identity = X509CertificateLoader.LoadPkcs12FromFile(certificateFile, password: null);
            using X509Certificate2 authorityCertificate = X509CertificateLoader.LoadCertificateFromFile(authorityFile);

            using X509Chain chain = new();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(authorityCertificate);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            if (!chain.Build(identity))
            {
                throw new InvalidOperationException($"The user certificate '{certificateFile}' is not signed by the authority certificate: {string.Join("; ", chain.ChainStatus.Select(status => status.StatusInformation.Trim()))}");
            }

            IReadOnlyList<string> names = PeerIdentity.ExtractCommonNames(identity.Subject);
            if (names.Count == 0)
            {
                throw new InvalidOperationException($"The user certificate '{certificateFile}' has no subject common name, so it names no user.");
            }

            string identified;
            try
            {
                identified = builder.Identify(names);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"The configuration handler could not tell which user the certificate '{certificateFile}' means: {ex.Message}", ex);
            }

            string? listed = network.Users.Keys.FirstOrDefault(user => string.Equals(user, identified, StringComparison.OrdinalIgnoreCase));
            return listed is null
                ? throw new InvalidOperationException($"The user certificate '{certificateFile}' is issued to {string.Join(", ", names)}, which identifies {(string.IsNullOrWhiteSpace(identified) ? "no one" : identified)}, not a user of the network.")
                : new NodeIdentity { UserName = listed, AuthorityFile = authorityFile, CertificateFile = certificateFile, CertificateThumbprint = identity.Thumbprint, AuthorityThumbprint = authorityCertificate.Thumbprint };
        }
        catch (Exception ex) when (ex is CryptographicException or IOException)
        {
            throw new InvalidOperationException($"A certificate file could not be read: {ex.Message}", ex);
        }
    }

    // The files do not change while the process runs, since they come from the host's statement and the command line, only what is in them does.
    private static (string? Authority, string? Certificate) FilesOf(EngineBuilder builder, NetworkConfig network)
        => (network.AuthorityCertificateOverride is { } authorityArgument ? Path.GetFullPath(authorityArgument) : builder.AuthorityCertificateValue is { } authority ? Absolute(authority, builder.AppDataFolder) : null,
            network.UserCertificateOverride is { } certificateArgument ? Path.GetFullPath(certificateArgument) : builder.UserCertificateValue is { } certificate ? Absolute(certificate, builder.AppDataFolder) : null);

    private static string Absolute(string path, string baseDirectory) => Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(baseDirectory, path));
}
