namespace BlueHeighliner.Comlink;

/// <summary>
/// The schema of the network configuration file: everything the engine needs to know about the users of one network, in one
/// place shared by every node, instead of per-node files or code. It is read from <c>Config.json</c> in the current working directory,
/// or, when the host allows command-line overrides (see <see cref="IEngineBuilder.CommandLineOverrides"/>), from the path given by the
/// <c>--config</c> argument; its absence is not an error unless <c>--config</c> names a file that does not exist.
/// The user this process runs as, for a node that should not ask for an install code, is named by the <c>--user</c> argument (again only when
/// overrides are allowed) or else a <c>User.json</c> in the current working directory does, holding <c>{ "User": "NAME" }</c> (or just the name as a JSON string).
/// </summary>
internal sealed class NetworkConfig
{
    private static readonly JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Subject name of the certificate authority trusted to sign every user's identity certificate. <see langword="null"/> uses the
    /// engine default (<c>COMLINK-ROOT</c>). Ignored when <see cref="AuthorityCertificate"/> is set.
    /// </summary>
    public string? TrustedAuthorityCertificateName { get; set; }

    /// <summary>
    /// Path to a public certificate file (for example <c>.cer</c>) for the certificate authority trusted to sign every user's identity
    /// certificate, used instead of a system certificate store lookup. A relative path is resolved against the directory containing
    /// the configuration file. Used together with <see cref="CertificateStore"/>.
    /// </summary>
    public string? AuthorityCertificate { get; set; }

    /// <summary>
    /// Path to a folder of PKCS#12 (<c>.pfx</c>) identity certificate files, one per user named <c>{USERNAME}.pfx</c>, that a node loads its own
    /// identity certificate and private key from instead of a system certificate store lookup. A relative path is resolved against
    /// the directory containing the configuration file. Used together with <see cref="AuthorityCertificate"/>.
    /// </summary>
    public string? CertificateStore { get; set; }

    /// <summary>
    /// Group definitions. Keys are group names; values are lists of member names, which may be user names or other group names,
    /// enabling nested hierarchies.
    /// </summary>
    public Dictionary<string, List<string>> UserGroups { get; set; } = [];

    /// <summary>Every user of the network, keyed by user name; names are matched case-insensitively.</summary>
    public Dictionary<string, NetworkUserConfig> Users { get; set; } = [];

    /// <summary>The user the process runs as, from the <c>--user</c> argument or else <c>User.json</c>; <see langword="null"/> when neither names one.</summary>
    public string? User { get; set; }

    /// <summary>Absolute directory containing the loaded file, used to resolve relative certificate paths. <see langword="null"/> when no file was loaded.</summary>
    private string? ConfigDirectory { get; set; }

    /// <summary>The arguments this configuration was loaded with, kept so it can be read again.</summary>
    private string[] arguments = [];

    /// <summary>The directory <c>Config.json</c> and <c>User.json</c> were looked for in, or <see langword="null"/> for the current working directory.</summary>
    private string? workingDirectory;

    /// <summary>
    /// Loads the network configuration: the file named by <c>--config</c>, else <c>Config.json</c> in the current working
    /// directory when it exists, else an empty configuration. The arguments are passed empty unless
    /// <see cref="IEngineController.CommandLineOverridesAllowed"/>.
    /// </summary>
    /// <param name="args">The process's command-line arguments.</param>
    /// <param name="workingDirectory">The directory to look for <c>Config.json</c> in. <see langword="null"/> (the default) is the current working directory.</param>
    public static NetworkConfig Load(string[] args, string? workingDirectory = null)
    {
        string defaultPath = Path.Combine(workingDirectory ?? Directory.GetCurrentDirectory(), "Config.json");
        int configIndex = Array.IndexOf(args, "--config");
        string? path = configIndex >= 0 && configIndex + 1 < args.Length ? args[configIndex + 1] : File.Exists(defaultPath) ? defaultPath : null;

        NetworkConfig config = new();
        if (path is not null)
        {
            config = JsonSerializer.Deserialize<NetworkConfig>(File.ReadAllText(path), jsonOptions) ?? new NetworkConfig();
            config.ConfigDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
        }

        int userIndex = Array.IndexOf(args, "--user");
        config.arguments = args;
        config.workingDirectory = workingDirectory;
        config.User = userIndex >= 0 && userIndex + 1 < args.Length ? args[userIndex + 1] : ReadUserFile(Path.Combine(workingDirectory ?? Directory.GetCurrentDirectory(), "User.json"));
        return config;
    }

    /// <summary>
    /// Reads the file again, from wherever it was loaded from, and replaces the trusted authority, certificate store, groups and users with what
    /// it now says. The user the process runs as (<see cref="User"/>) is kept, since a running process does not become another user. If the file
    /// can no longer be read or parsed this throws and leaves the current contents as they were.
    /// </summary>
    public void Reload()
    {
        NetworkConfig fresh = Load(arguments, workingDirectory);
        TrustedAuthorityCertificateName = fresh.TrustedAuthorityCertificateName;
        AuthorityCertificate = fresh.AuthorityCertificate;
        CertificateStore = fresh.CertificateStore;
        UserGroups = fresh.UserGroups;
        Users = fresh.Users;
        ConfigDirectory = fresh.ConfigDirectory;
    }

    private static string? ReadUserFile(string path)
    {
        if (!File.Exists(path)) { return null; }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement switch
        {
            { ValueKind: JsonValueKind.String } name => name.GetString(),
            { ValueKind: JsonValueKind.Object } root when root.EnumerateObject().FirstOrDefault(property => property.NameEquals("User") || string.Equals(property.Name, "User", StringComparison.OrdinalIgnoreCase)) is { Value.ValueKind: JsonValueKind.String } property => property.Value.GetString(),
            _ => null
        };
    }

    /// <summary>Returns the entry for <paramref name="userName"/>, or <see langword="null"/> when the network does not list that user.</summary>
    public NetworkUserConfig? Find(string? userName) => userName is null ? null : Users.FirstOrDefault(user => string.Equals(user.Key, userName, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>Returns what the network says about <paramref name="userName"/> as an engine <see cref="UserInfo"/>, or <see langword="null"/> when the user is not listed.</summary>
    public UserInfo? GetUserInfo(string userName)
        => Find(userName) is { } user
            ? new UserInfo
            {
                Name = Users.Keys.First(key => string.Equals(key, userName, StringComparison.OrdinalIgnoreCase)),
                Role = user.GetRole(),
                PeerPort = user.PeerPort,
                InterfacePort = user.InterfacePort,
                OutgoingPoints = [.. user.OutgoingPoints.Select(point => point.ToPoint())],
                ChildClients = user.ChildClients,
                StoresMessages = user.StoresMessages,
                SecurityLevel = user.SecurityLevel,
                CertificateName = user.CertificateName,
                Data = new Dictionary<string, string>(user.Data),
                Groups = [.. UserGroups.Where(group => group.Value.Contains(userName, StringComparer.OrdinalIgnoreCase)).Select(group => group.Key)]
            }
            : null;

    /// <summary>Resolves <see cref="AuthorityCertificate"/> against the directory of the loaded file when it is a relative path.</summary>
    public string? GetAuthorityCertificatePath() => Resolve(AuthorityCertificate);

    /// <summary>Returns where <paramref name="userName"/>'s identity certificate file is, <c>{CertificateStore}/{USERNAME}.pfx</c>, or <see langword="null"/> when there is no <see cref="CertificateStore"/>.</summary>
    public string? GetCertificatePath(string userName) => Resolve(CertificateStore) is { } store ? Path.Combine(store, $"{userName}.pfx") : null;

    private string? Resolve(string? path)
        => path is null || ConfigDirectory is null || Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(ConfigDirectory, path));
}

/// <summary>Everything the network file says about one user: the parts that become their <see cref="UserInfo"/>, and settings of the node they run.</summary>
internal sealed class NetworkUserConfig
{
    /// <summary>Networking role of a node this user runs: <c>"Peer"</c>, <c>"Client"</c>, <c>"Server"</c> or <c>"Relay"</c> (case-insensitive). <see langword="null"/> or unrecognized is <c>"Peer"</c>.</summary>
    public string? Role { get; init; }

    /// <summary>TCP port a node this user runs listens on for IP connections. <see langword="null"/> uses the engine default (50021).</summary>
    public int? PeerPort { get; init; }

    /// <summary>Loopback TCP port of the local interface listener. <see langword="null"/> uses the engine default (50020).</summary>
    public int? InterfacePort { get; init; }

    /// <summary>The points a node this user runs connects out to and keeps connected: IP hosts and ports to dial, serial ports to open. A client uses the first as its server.</summary>
    public List<ConnectionPointConfig> OutgoingPoints { get; init; } = [];

    /// <summary>For a server, the client users that belong to it.</summary>
    public List<string> ChildClients { get; init; } = [];

    /// <summary>For a server, whether it stores the messages it routes and answers retrieval requests.</summary>
    public bool StoresMessages { get; init; }

    /// <summary>The name of the security level this user runs at. <see langword="null"/> is the lowest configured level.</summary>
    public string? SecurityLevel { get; init; }

    /// <summary>Certificate subject name of this user. <see langword="null"/> is the user name itself.</summary>
    public string? CertificateName { get; init; }

    /// <summary>App-specific string keys and values attached to this user; the engine does not interpret them.</summary>
    public Dictionary<string, string> Data { get; init; } = [];

    /// <summary>Run headless, as a normal peer with no GUI, when this user is the one the process is launched as (see <see cref="NetworkConfig.User"/>).</summary>
    public bool Headless { get; init; }

    /// <summary>Text shown in the title bar's alert box while alarming. <see langword="null"/> uses the engine default (<c>"ALERT"</c>).</summary>
    public string? AlertText { get; init; }

    /// <summary>Seconds the alarm sound plays after an alert is received. <see langword="null"/> uses the engine default (30).</summary>
    public double? AlarmSoundSeconds { get; init; }

    /// <summary>Whether clicking the alert box, or pressing Space or Enter outside a text input, confirms the latest unconfirmed alert. <see langword="null"/> uses the engine default (<see langword="true"/>).</summary>
    public bool? QuickConfirmationEnabled { get; init; }

    /// <summary>Whether the draft editor's alert checkbox is shown. <see langword="null"/> uses the engine default (<see langword="true"/>).</summary>
    public bool? ComposeAlertsEnabled { get; init; }

    /// <summary>Whether message tags are shown anywhere in the UI. <see langword="null"/> uses the engine default (<see langword="true"/>).</summary>
    public bool? MessageTagsEnabled { get; init; }

    /// <summary>Label used for the tag input's watermark in the draft editor. <see langword="null"/> or empty uses the engine default (<c>"Tag"</c>).</summary>
    public string? MessageTagLabel { get; init; }

    /// <summary>Whether the print manager's "print received" toggle starts enabled. <see langword="null"/> uses the engine default (<see langword="false"/>).</summary>
    public bool? PrintReceivedEnabled { get; init; }

    /// <summary>Parses <see cref="Role"/>, or <see langword="null"/> when unset or unrecognized.</summary>
    public UserRole? GetRole() => Enum.TryParse(Role, ignoreCase: true, out UserRole role) ? role : null;
}

/// <summary>JSON deserialization shape for an outgoing connection point in the network file.</summary>
internal sealed class ConnectionPointConfig
{
    /// <summary>IPv4 or IPv6 address of the remote node. Ignored when <see cref="SerialPort"/> is set.</summary>
    public string IpAddress { get; init; } = string.Empty;
    /// <summary>TCP port of the remote node's listener. Ignored when <see cref="SerialPort"/> is set.</summary>
    public int Port { get; init; }
    /// <summary>Name of the local MicroGate serial port cabled to the remote node; when set, this point is reached over serial instead of IP.</summary>
    public string? SerialPort { get; init; }
    /// <summary>HDLC station address of this node on the serial link. Defaults to 255 (0xFF). Must differ from <see cref="RemoteSerialAddress"/>; the other end of the cable uses the two values the other way round.</summary>
    public byte SerialAddress { get; init; } = 0xFF;
    /// <summary>HDLC station address of the node at the other end of the serial link. Defaults to 254 (0xFE). Must differ from <see cref="SerialAddress"/>.</summary>
    public byte RemoteSerialAddress { get; init; } = 0xFE;
    /// <summary>For a serial point, the user at the other end of the cable. <see langword="null"/> names the user after the port.</summary>
    public string? User { get; init; }

    /// <summary>Converts this entry to the engine's connection point model.</summary>
    public ConnectionPoint ToPoint() => new() { IpAddress = IpAddress, Port = Port, SerialPort = SerialPort, SerialAddress = SerialAddress, RemoteSerialAddress = RemoteSerialAddress, User = User };
}
