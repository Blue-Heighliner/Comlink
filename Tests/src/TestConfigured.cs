namespace BlueHeighliner.Comlink.Tests;

/// <summary>Builds <see cref="ConfiguredEngineController"/>s for tests that have no use for the node's certificates.</summary>
internal static class TestConfigured
{
    /// <summary>Creates the controller, with an identity that names no certificate files unless <paramref name="identity"/> is given.</summary>
    /// <param name="fallback">The wrapped controller.</param>
    /// <param name="config">The network configuration.</param>
    /// <param name="user">The current user.</param>
    /// <param name="identity">The node's identity, or <see langword="null"/> for one with no files.</param>
    public static ConfiguredEngineController Create(IEngineController fallback, NetworkConfig config, ICurrentUserProvider user, NodeIdentity? identity = null)
        => new(fallback, config, user, identity ?? Identity("", "", user.UserName ?? string.Empty));

    /// <summary>Creates an identity for the given files.</summary>
    /// <param name="authorityFile">The authority certificate file.</param>
    /// <param name="certificateFile">The user certificate file.</param>
    /// <param name="userName">The user the certificate names.</param>
    public static NodeIdentity Identity(string authorityFile, string certificateFile, string userName)
        => new() { UserName = userName, AuthorityFile = authorityFile, CertificateFile = certificateFile, CertificateThumbprint = string.Empty, AuthorityThumbprint = string.Empty };
}
