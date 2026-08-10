namespace BlueHeighliner.Comlink.Engine.Models;

/// <summary>Network address and port for connecting to a remote site.</summary>
public sealed class SiteEndpoint
{
    /// <summary>IP address of the remote site.</summary>
    public required string IpAddress { get; init; }
    /// <summary>TCP port of the remote site.</summary>
    public required int Port { get; init; }
}
