namespace BlueHeighliner.Comlink;

/// <summary>Describes one server user's position in a client/server hierarchy: the child users that belong to it.</summary>
internal sealed record ServerUserConfig
{
    /// <summary>Names of the users that belong to this server: its clients.</summary>
    public required IReadOnlyList<string> Children { get; init; }
}
