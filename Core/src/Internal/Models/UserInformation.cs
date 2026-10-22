namespace BlueHeighliner.Comlink;

/// <summary>Everything the engine itself knows about one user, with the level and profile as the plain objects the network configuration file produces. The host never sees it: what it is handed is the <see cref="UserInfo{TLevel, TProfile}"/> typed from it.</summary>
internal sealed record UserInformation : IUserInfo
{
    /// <summary>Canonical name of the user. By convention user names are all uppercase.</summary>
    public required string Name { get; init; }
    /// <summary>
    /// The level (see <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Level"/>) this user runs at, a member of the enum the host stated for its levels, as the network configuration
    /// file names it. <see langword="null"/> (the default) is the lowest configured level.
    /// </summary>
    public Enum? Level { get; init; }
    /// <summary>Names of the groups this user is a member of.</summary>
    public IReadOnlyList<string> Groups { get; init; } = [];
    /// <summary>The networking role of a node this user runs. <see langword="null"/> (the default) is <see cref="UserRole.Client"/>.</summary>
    public UserRole? Role { get; init; }
    /// <summary>The IP address or host name other nodes connect to in order to reach a node this user runs. <see langword="null"/> (the default) when it is not reachable that way, so no one dials it unless a link says to, and then at the loopback address.</summary>
    public string? IpHost { get; init; }
    /// <summary>The TCP port a node this user runs listens on for MSMT connections, and that nodes dialing it connect to. <see langword="null"/> (the default) is 50021.</summary>
    public int? MsmtPort { get; init; }
    /// <summary>The HDLC station address of a node this user runs: the local address it uses, and the remote address other nodes use to connect to it. <see langword="null"/> (the default) is 1. Users that are HDLC-linked need distinct addresses.</summary>
    public byte? HdlcAddress { get; init; }
    /// <summary>The names of the MicroGate ports a node this user runs opens to form HDLC connections, or a single <c>*</c> for every port available on the machine. Empty (the default) for none.</summary>
    public IReadOnlyList<string> HdlcPorts { get; init; } = [];
    /// <summary>The loopback TCP port a node this user runs uses for its local interface listener. <see langword="null"/> (the default) is 50020.</summary>
    public int? InterfacePort { get; init; }
    /// <summary>The link to this user's parent: the user it forms an outgoing connection with by default. <see langword="null"/> (the default) for a user with no parent.</summary>
    public UserLink? Parent { get; init; }
    /// <summary>The links to this user's children, the users it listens for by default. They are the clients of a <see cref="UserRole.Server"/>. Empty by default.</summary>
    public IReadOnlyList<UserLink> Children { get; init; } = [];
    /// <summary>The names of the auto forwarders this user has access to: the options of the auto forward screen they can open and keep a target list in. Empty by default.</summary>
    public IReadOnlyList<string> AutoForwarders { get; init; } = [];
    /// <summary>The user's profile as written in the network configuration, read into the type the host stated in its <c>Types</c> call, or <see langword="null"/> when it stated <see cref="NoProfile"/> or the user has no <c>Profile</c> key.</summary>
    public object? Profile { get; init; }
}
