namespace BlueHeighliner.Comlink.Models;

/// <summary>Everything the engine knows about one user: who they are and, for a user that runs a node, how that node takes part in the network.</summary>
public sealed class UserInfo
{
    /// <summary>Canonical name of the user. By convention user names are all uppercase.</summary>
    public required string Name { get; init; }
    /// <summary>
    /// The name of the security level (see <see cref="IEngineBuilder.SecurityLevels"/>) this user runs at. <see langword="null"/> (the
    /// default) is the lowest configured level.
    /// </summary>
    public string? SecurityLevel { get; init; }
    /// <summary>
    /// The certificate subject name that belongs to this user: for the local user the identity certificate to look up, and for
    /// others the name their certificate is expected to carry. <see langword="null"/> (the default) is the user name itself.
    /// </summary>
    public string? CertificateName { get; init; }
    /// <summary>App-specific data attached to the user. The engine does not interpret it; it travels with the user's <see cref="UserIdentity"/>. Empty by default.</summary>
    public IReadOnlyDictionary<string, string> Data { get; init; } = new Dictionary<string, string>();
    /// <summary>
    /// For a <see cref="UserRole.Server"/> user, whether it keeps a copy of every message it routes and answers a client's retrieval
    /// request (the client's RETRIEVE screen, see <see cref="IFrameBuilder{TFrame}.Retrieval"/>) with a copy of each stored message that fits.
    /// Every node on a network must describe the server alike, since a client learns which servers store from this same info.
    /// </summary>
    public bool StoresMessages { get; init; }
    /// <summary>Names of the groups this user is a member of.</summary>
    public IReadOnlyList<string> Groups { get; init; } = [];
    /// <summary>The networking role of a node this user runs. <see langword="null"/> (the default) is <see cref="UserRole.Peer"/>.</summary>
    public UserRole? Role { get; init; }
    /// <summary>The TCP port a node this user runs listens on for IP connections from other nodes. <see langword="null"/> (the default) is 50021.</summary>
    public int? PeerPort { get; init; }
    /// <summary>The loopback TCP port a node this user runs uses for its local interface listener. <see langword="null"/> (the default) is 50020.</summary>
    public int? InterfacePort { get; init; }
    /// <summary>The points a node this user runs connects out to and keeps connected: IP hosts and ports to dial, serial ports to open. A <see cref="UserRole.Client"/> connects to the first only. None by default.</summary>
    public IReadOnlyList<ConnectionPoint> OutgoingPoints { get; init; } = [];
    /// <summary>For a <see cref="UserRole.Server"/> user, the client users that belong to it. Empty by default.</summary>
    public IReadOnlyList<string> ChildClients { get; init; } = [];
}
