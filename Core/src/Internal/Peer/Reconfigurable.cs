namespace BlueHeighliner.Comlink;

/// <summary>A running peer service that can bring itself in line with the configuration as it is now without being restarted, touching only what changed.</summary>
internal interface IReconfigurable
{
    /// <summary>
    /// Applies the changes in the configuration since the service started or was last reconfigured: a changed listen port restarts only the listener,
    /// outgoing points that are no longer defined are closed and newly defined ones opened, and connections to points that did not change are left as
    /// they are. Does nothing before the service has started, or when nothing changed.
    /// </summary>
    void Reconfigure();
}
