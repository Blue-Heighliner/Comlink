namespace BlueHeighliner.Comlink.Models;

/// <summary>Describes one server user's position in a client/server hierarchy: the child client users that belong to it.</summary>
public sealed record ServerUserConfig
{
    /// <summary>Names of the client users that belong to this server.</summary>
    public required IReadOnlyList<string> ChildClients { get; init; }
}
