namespace BlueHeighliner.Comlink;

/// <summary>
/// Simplified, synchronous snapshot of the engine handed to every processor method, exposing only what a processor needs rather than the full
/// <see cref="IEngineConnection"/> surface: this instance's own identity, the full user directory and who is currently reachable. Each
/// <see cref="Users"/>/<see cref="ConnectedUsers"/> value carries that user's directly-assigned group memberships, the same as
/// <see cref="UserInfo.Groups"/>, but never a real installation code (empty), since that only ever belongs to this instance's own <see cref="CurrentUser"/>.
/// </summary>
public interface IEngineContext
{
    /// <summary>Gets this instance's own installed user. Never <see langword="null"/>: a processor only ever runs once a user is installed.</summary>
    UserInfo CurrentUser { get; }

    /// <summary>Gets every known user in the messaging system (the users in the network configuration file), by name.</summary>
    IReadOnlyDictionary<string, UserInfo> Users { get; }

    /// <summary>Gets the subset of <see cref="Users"/> currently reachable over at least one live peer connection, by name. Whether a given user is connected is whether it is a key here.</summary>
    IReadOnlyDictionary<string, UserInfo> ConnectedUsers { get; }

    /// <summary>Returns the names of the users that belong to the group <paramref name="groupName"/>, groups within it expanded, or an empty list when there is no such group.</summary>
    /// <param name="groupName">The group to expand.</param>
    IReadOnlyList<string> GetGroupMembers(string groupName);
}
