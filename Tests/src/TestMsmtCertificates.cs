namespace BlueHeighliner.Comlink.Tests;

/// <summary>Generates an ephemeral, in-memory certificate authority and leaf certificates for MSMT TLS tests.</summary>
internal static class TestMsmtCertificates
{
    /// <summary>Creates a server certificate, a client certificate, and the CA collection that trusts both, each leaf valid for the loopback address so a real TLS handshake over <c>127.0.0.1</c> passes hostname validation.</summary>
    /// <returns>The generated server certificate, client certificate, and trusted authority collection.</returns>
    public static (X509Certificate2 Server, X509Certificate2 Client, X509Certificate2Collection TrustedAuthorities) Create()
    {
        (X509Certificate2 authority, RSA authorityKey) = CreateAuthority();
        using (authorityKey)
        {
            X509Certificate2 server = CreateLeaf(authority, authorityKey, "CN=msmt-server");
            X509Certificate2 client = CreateLeaf(authority, authorityKey, "CN=msmt-client");

            return (server, client, [authority]);
        }
    }

    private static (X509Certificate2 Authority, RSA AuthorityKey) CreateAuthority()
    {
        RSA authorityKey = RSA.Create(2048);
        CertificateRequest authorityRequest = new("CN=Test MSMT CA", authorityKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        authorityRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        X509Certificate2 authority = authorityRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));

        return (authority, authorityKey);
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 authority, RSA authorityKey, string subject)
    {
        RSA leafKey = RSA.Create(2048);
        CertificateRequest request = new(subject, leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        SubjectAlternativeNameBuilder sanBuilder = new();
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(sanBuilder.Build());

        byte[] serialNumber = RandomNumberGenerator.GetBytes(16);
        using X509Certificate2 leaf = request.Create(
            authority.SubjectName,
            X509SignatureGenerator.CreateForRSA(authorityKey, RSASignaturePadding.Pkcs1),
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1),
            serialNumber);

        return leaf.CopyWithPrivateKey(leafKey);
    }
}
