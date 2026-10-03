namespace BlueHeighliner.Comlink.Models;

/// <summary>Describes one server user's position in a client/server hierarchy: the child client users that belong to it, and the relays among them with the clients behind each.</summary>
internal sealed record ServerUserConfig
{
    /// <summary>Names of the client users that belong to this server.</summary>
    public required IReadOnlyList<string> ChildClients { get; init; }
    /// <summary>Gets, for each relay among <see cref="ChildClients"/>, the client users that sit behind it. Traffic for one of them goes to the relay, which forwards it on.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Relays { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
}
