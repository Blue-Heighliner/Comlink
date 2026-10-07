namespace BlueHeighliner.Comlink;

/// <summary>Re-reads the configuration files while the engine runs (the network file, <c>Logging.json</c> and <c>User.json</c>) and applies what changed.</summary>
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
    /// <remarks>
    /// <c>Logging.json</c> is read again too, so the log categories it turns on take effect at once, and so is <c>User.json</c>: when it now names another user, or none, the current user changes
    /// (see <see cref="IUserService.Refresh"/>), and the connections are then started again for the new user instead of being adjusted.
    /// </remarks>
    /// <exception cref="Exception">The network file can no longer be read or parsed; the engine keeps running on the configuration it had.</exception>
    Task Reload();
}

/// <inheritdoc />
internal sealed class NetworkReloadService(
    NetworkConfig network,
    IEngineController engineController,
    IRolePeerService peerService,
    IInterfaceService interfaceService,
    ILogSettings logSettings,
    IUserService userService,
    ILoggerFactory loggerFactory) : INetworkReloadService
{
    private readonly ILogger logger = loggerFactory.CreateLogger(LogCategories.App);

    /// <inheritdoc />
    public event Action? Reloaded;

    /// <inheritdoc />
    public async Task Reload()
    {
        string restartBefore = RestartsPeers();
        string interfaceBefore = InterfaceSettings();

        network.Reload();
        logSettings.Reload();
        logger.Record(LogEvents.NetworkConfigurationReloaded, "Network configuration reloaded");

        // A new user's connections are started again from scratch, which reads the new configuration anyway.
        if (!await userService.Refresh()) { Apply(restartBefore, interfaceBefore); }

        Reloaded?.Invoke();
    }

    private void Apply(string restartBefore, string interfaceBefore)
    {
        if (restartBefore != RestartsPeers())
        {
            logger.Record(LogEvents.PeersRestarting, "Role or certificates changed, restarting connections");
            peerService.Restart();
        }
        else
        {
            peerService.Reconfigure();
        }

        if (interfaceBefore != InterfaceSettings())
        {
            logger.Record(LogEvents.InterfaceRestarting, "Interface listener changed, restarting it");
            interfaceService.Restart();
        }
    }

    // What cannot be changed on a running implementation: a different role is a different implementation, and the certificates are fixed when it is created.
    // Everything else about peer connections (the listen port, the outgoing points, the server topology) is applied in place by Reconfigure.
    private string RestartsPeers() => string.Join('|', engineController.Role, CertificateSettings());

    private string InterfaceSettings() => string.Join('|', engineController.InterfacePort, CertificateSettings());

    private string CertificateSettings()
        => string.Join('|',
            network.CertificateStore,
            network.AuthorityCertificate);
}
