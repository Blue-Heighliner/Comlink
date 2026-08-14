namespace BlueHeighliner.Comlink.Engine.Models;

/// <summary>
/// Describes one server user's position in a client/server hierarchy: the endpoint it listens on and
/// forms server-to-server connections through, and the child client users that belong to it.
/// </summary>
public sealed record ServerUserConfig
{
    /// <summary>Endpoint this server user listens on for connections from its child clients and other servers.</summary>
    public required UserEndpoint Endpoint { get; init; }
    /// <summary>Names of the client users that belong to this server.</summary>
    public required IReadOnlyList<string> ChildClients { get; init; }
}
