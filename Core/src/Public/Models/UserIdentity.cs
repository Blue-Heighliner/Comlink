namespace BlueHeighliner.Comlink;

/// <summary>Who a connection's remote node is: the name of the user it is with.</summary>
public sealed record UserIdentity
{
    /// <summary>Gets the user name the connection is with. Messages for this user are sent over the connection.</summary>
    public required string Name { get; init; }
}
