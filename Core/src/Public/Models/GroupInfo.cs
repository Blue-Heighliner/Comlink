namespace BlueHeighliner.Comlink;

/// <summary>A group of users in the network configuration file, as the <see cref="IUserCatalog{TLevel, TProfile}"/> lists it.</summary>
public sealed record GroupInfo
{
    /// <summary>Gets the group's id: its name in the network configuration file, which is also its key in <see cref="IUserCatalog{TLevel, TProfile}.Groups"/>.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the names of the users that belong to the group, with the groups it contains expanded, so every entry is a user.</summary>
    public required IReadOnlyList<string> Members { get; init; }
}
