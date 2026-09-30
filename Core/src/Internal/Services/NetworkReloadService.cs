namespace BlueHeighliner.Comlink.Services;

/// <summary>Re-reads the network configuration file while the engine runs and applies what changed.</summary>
internal interface INetworkReloadService
{
    /// <summary>Raised after the file has been read again and whatever needed restarting has been restarted, so the UI can pick up the new values.</summary>
    event Action? Reloaded;

    /// <summary>
    /// Reads the network configuration file again, then changes only what the new file makes necessary. A changed listen port restarts only the
    /// listener, outgoing points no longer defined are closed and newly defined ones opened while the others keep their connections, and a server's
    /// topology is updated in place; only a changed role or changed certificates restart the peer layer, and only a changed port or certificates
    /// restart the local interface listener. Everything else, such as users, groups, security levels and node settings, is read on demand and so is
    /// current as soon as the file has been read.
    /// </summary>
    /// <exception cref="Exception">The file can no longer be read or parsed; the engine keeps running on the configuration it had.</exception>
    void Reload();
}

/// <inheritdoc />
internal sealed class NetworkReloadService(
    NetworkConfig network,
    IEngineController engineController,
    ICurrentUserProvider currentUserProvider,
    IRolePeerService peerService,
    IInterfaceService interfaceService,
    ILoggerFactory loggerFactory) : INetworkReloadService
{
    private readonly ILogger logger = loggerFactory.CreateLogger("ACTIVITY");

    /// <inheritdoc />
    public event Action? Reloaded;

    /// <inheritdoc />
    public void Reload()
    {
        string restartBefore = RestartsPeers();
        string interfaceBefore = InterfaceSettings();

        network.Reload();
        logger.LogInformation("Network configuration reloaded");

        if (restartBefore != RestartsPeers())
        {
            logger.LogInformation("Role or certificates changed, restarting connections");
            peerService.Restart();
        }
        else
        {
            peerService.Reconfigure();
        }

        if (interfaceBefore != InterfaceSettings())
        {
            logger.LogInformation("Interface listener changed, restarting it");
            interfaceService.Restart();
        }

        Reloaded?.Invoke();
    }

    // What cannot be changed on a running implementation: a different role is a different implementation, and the certificates are fixed when it is created.
    // Everything else about peer connections (the listen port, the outgoing points, the server topology) is applied in place by Reconfigure.
    private string RestartsPeers() => string.Join('|', engineController.Role, CertificateSettings());

    private string InterfaceSettings() => string.Join('|', engineController.InterfacePort, CertificateSettings());

    private string CertificateSettings()
        => string.Join('|',
            network.CertificateStore,
            network.AuthorityCertificate,
            engineController.TrustedAuthorityCertificateName,
            currentUserProvider.UserName is { } user ? engineController.GetCertificateName(user) : null);
}
