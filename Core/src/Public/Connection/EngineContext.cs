namespace BlueHeighliner.Comlink;

/// <summary>
/// Simplified, synchronous snapshot of the engine handed to every processor method, exposing only what a processor needs rather than the full
/// <see cref="IServiceConnection"/> surface: this instance's own identity, the full user directory and who is currently reachable. Each
/// <see cref="Users"/>/<see cref="ConnectedUsers"/> entry carries that user's directly-assigned group memberships, the same as
/// <see cref="UserInfo.Groups"/>, but never a real installation code (empty), since that only ever belongs to this instance's own <see cref="CurrentUser"/>.
/// </summary>
public interface IEngineContext
{
    /// <summary>Gets this instance's own installed user. Never <see langword="null"/>: a processor only ever runs once a user is installed.</summary>
    UserInfo CurrentUser { get; }

    /// <summary>Gets every known user in the messaging system (see <see cref="IEngineBuilder.Users"/>), computed lazily as enumerated rather than materialized upfront.</summary>
    IEnumerable<UserInfo> Users { get; }

    /// <summary>Gets the subset of <see cref="Users"/> currently reachable over at least one live peer connection, computed lazily as enumerated rather than materialized upfront.</summary>
    IEnumerable<UserInfo> ConnectedUsers { get; }

    /// <summary>Returns whether <paramref name="userName"/> is currently reachable over at least one live peer connection.</summary>
    /// <param name="userName">The user to look up.</param>
    bool IsConnected(string userName);
}
