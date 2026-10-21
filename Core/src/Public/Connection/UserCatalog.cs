namespace BlueHeighliner.Comlink;

/// <summary>
/// Who is on the network: the user this node runs as, every user the network configuration file lists, the groups they belong to and who is reachable right now.
/// It is a service, not part of what a handler is handed: a handler or any other component asks for it through dependency injection. Every property reads the
/// current state each time, so a value obtained earlier is a snapshot and one obtained again reflects a refreshed configuration or a connection that came or went.
/// </summary>
public interface IUserCatalog
{
    /// <summary>Gets the user this node runs as, the one its certificate names.</summary>
    /// <exception cref="InvalidOperationException">The node has no current user, which is only so while its certificate cannot be used and the engine shows its error screen; no handler runs then.</exception>
    UserInfo CurrentUser { get; }

    /// <summary>Gets every known user in the messaging system (the users in the network configuration file), by name. Each carries the groups it is directly a member of, the same as <see cref="UserInfo.Groups"/>.</summary>
    IReadOnlyDictionary<string, UserInfo> Users { get; }

    /// <summary>Gets the subset of <see cref="Users"/> currently reachable over at least one live peer connection, by name. Whether a given user is connected is whether it is a key here.</summary>
    IReadOnlyDictionary<string, UserInfo> ConnectedUsers { get; }

    /// <summary>Gets every group in the network configuration file, by id.</summary>
    IReadOnlyDictionary<string, GroupInfo> Groups { get; }
}
