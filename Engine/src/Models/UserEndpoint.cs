namespace BlueHeighliner.Comlink.Engine.Models;

/// <summary>Network address and port for connecting to a remote user.</summary>
public sealed class UserEndpoint
{
    /// <summary>IP address of the remote user.</summary>
    public required string IpAddress { get; init; }
    /// <summary>TCP port of the remote user.</summary>
    public required int Port { get; init; }
}
