namespace BlueHeighliner.Comlink.Models;

/// <summary>Who a connection's remote node is: a user name plus any app-specific information attached to that user.</summary>
public sealed record UserIdentity
{
    /// <summary>Gets the user name the connection is with. Messages for this user are sent over the connection.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the app-specific information attached to the user; see <see cref="IEngineBuilder.UserData(string, IReadOnlyDictionary{string, string})"/>.</summary>
    public IReadOnlyDictionary<string, string> Data { get; init; } = new Dictionary<string, string>();
}
