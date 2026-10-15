namespace BlueHeighliner.Comlink;

/// <summary>Who this node is and the certificate files it proves it with, resolved at startup and again by every refresh (see <see cref="MsmtCertificateLookup.ResolveIdentity"/>), so a changed certificate is noticed.</summary>
internal sealed record NodeIdentity
{
    /// <summary>Gets the user the process runs as, spelled as the network configuration file spells it: a subject common name of the user certificate that the network lists.</summary>
    public required string UserName { get; init; }

    /// <summary>Gets the absolute path of the authority certificate file, the only authority trusted.</summary>
    public required string AuthorityFile { get; init; }

    /// <summary>Gets the absolute path of the user's PKCS#12 identity certificate file.</summary>
    public required string CertificateFile { get; init; }

    /// <summary>Gets the thumbprint of the user certificate, which changes when the certificate is replaced.</summary>
    public required string CertificateThumbprint { get; init; }

    /// <summary>Gets the thumbprint of the authority certificate, which changes when the authority is replaced.</summary>
    public required string AuthorityThumbprint { get; init; }
}

/// <summary>What checking the node's certificates found: who the node is, or why that cannot be told, in which case the UI shows the problem instead of the app.</summary>
internal sealed record IdentityCheck
{
    /// <summary>Gets who the node is. When <see cref="Problem"/> is set this names no user and holds only the certificate files that are known, so a later refresh can use them.</summary>
    public required NodeIdentity Identity { get; init; }

    /// <summary>Gets why the node cannot be identified, or <see langword="null"/> when it can.</summary>
    public string? Problem { get; init; }
}
