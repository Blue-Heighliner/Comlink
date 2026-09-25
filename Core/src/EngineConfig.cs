namespace BlueHeighliner.Comlink;

/// <summary>Configuration loaded from a JSON file via the <c>--config</c> command-line argument.</summary>
public sealed class EngineConfig
{
    private static readonly JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Run headless — as a normal peer client, with no GUI — instead of launching the desktop GUI.</summary>
    public bool HeadlessMode { get; init; }

    /// <summary>Debug user name override; skips <c>State.json</c> lookup when set.</summary>
    public string? UserName { get; init; }

    /// <summary>Peer TCP listen port override. <see langword="null"/> uses the Engine default (50021).</summary>
    public int? PeerPort { get; init; }

    /// <summary>Interface TCP listen port override. <see langword="null"/> uses the Engine default (50020).</summary>
    public int? InterfacePort { get; init; }

    /// <summary>
    /// Custom app data directory. <see langword="null"/> uses <c>%APPDATA%\{AppName}</c>.
    /// A path starting with <c>@</c> is relative to that default location.
    /// </summary>
    public string? DataFolder { get; init; }

    /// <summary>
    /// TLS identity certificate subject name for peer authentication, mandatory for every MSMT connection.
    /// <see langword="null"/> uses the Engine default (the user name itself, unprefixed); an explicit name
    /// uses that certificate instead. Ignored when <see cref="PeerCertificateFile"/> is set.
    /// </summary>
    public string? PeerCertificateName { get; init; }

    /// <summary>
    /// TLS certificate authority subject name trusted to sign every peer's identity certificate.
    /// <see langword="null"/> uses the Engine default (<c>COMLINK-ROOT</c>); an explicit name uses that
    /// certificate instead. Ignored when <see cref="TrustedAuthorityCertificateFile"/> is set.
    /// </summary>
    public string? TrustedAuthorityCertificateName { get; init; }

    /// <summary>
    /// Path to a PKCS#12 (<c>.pfx</c>) file containing the identity certificate and its private key, used
    /// instead of a system certificate store lookup. A relative path is resolved against the directory
    /// containing the config file itself. Must be set together with <see cref="TrustedAuthorityCertificateFile"/>.
    /// <see langword="null"/> (the default) uses <see cref="PeerCertificateName"/> against the system store instead.
    /// </summary>
    public string? PeerCertificateFile { get; init; }

    /// <summary>
    /// Path to a public certificate file (e.g. <c>.cer</c>) for the certificate authority trusted to sign
    /// every peer's identity certificate, used instead of a system certificate store lookup. A relative
    /// path is resolved against the directory containing the config file itself. Must be set together with
    /// <see cref="PeerCertificateFile"/>. <see langword="null"/> (the default) uses
    /// <see cref="TrustedAuthorityCertificateName"/> against the system store instead.
    /// </summary>
    public string? TrustedAuthorityCertificateFile { get; init; }

    /// <summary>Text shown in the title bar's alert box while alarming. <see langword="null"/> uses the Engine default (<c>"ALERT"</c>).</summary>
    public string? AlertText { get; init; }

    /// <summary>
    /// Seconds the alarm sound plays after an alert is received before automatically stopping; resets
    /// whenever a new alert is received. <see langword="null"/> uses the Engine default (30).
    /// </summary>
    public double? AlarmSoundSeconds { get; init; }

    /// <summary>
    /// Whether clicking the alert box, or pressing Space/Enter while not focused in a text input, confirms
    /// (marks read) the latest unconfirmed alert. <see langword="null"/> uses the Engine default (<see langword="true"/>).
    /// </summary>
    public bool? QuickConfirmationEnabled { get; init; }

    /// <summary>
    /// Whether the draft editor's alert checkbox is shown, letting the user mark and send a draft as an
    /// alert. <see langword="null"/> uses the Engine default (<see langword="true"/>). Disabling this never
    /// prevents receiving and alarming on alerts sent by a peer.
    /// </summary>
    public bool? ComposeAlertsEnabled { get; init; }

    /// <summary>
    /// Whether message tags are shown anywhere in the UI (draft tag input, entry listing tag label).
    /// <see langword="null"/> uses the Engine default (<see langword="true"/>).
    /// </summary>
    public bool? MessageTagsEnabled { get; init; }

    /// <summary>
    /// Label used for the tag input's watermark in the draft editor. <see langword="null"/> or empty uses
    /// the Engine default (<c>"Tag"</c>).
    /// </summary>
    public string? MessageTagLabel { get; init; }

    /// <summary>
    /// Whether the print manager's "print received" toggle starts enabled, automatically adding every
    /// received message to the print queue. <see langword="null"/> uses the Engine default (<see langword="false"/>).
    /// </summary>
    public bool? PrintReceivedEnabled { get; init; }

    /// <summary>
    /// User definitions and endpoint overrides. Keys are user names (case-insensitive).
    /// Entries may override an existing user's endpoint or introduce an entirely new user.
    /// </summary>
    public Dictionary<string, UserEndpointConfig> Users { get; init; } = [];

    /// <summary>
    /// User group definitions. Keys are group names; values are lists of member names.
    /// Members may be user names or other group names, enabling nested hierarchies.
    /// </summary>
    public Dictionary<string, List<string>> UserGroups { get; init; } = [];

    /// <summary>
    /// Networking topology role: <c>"Peer"</c>, <c>"Client"</c>, or <c>"Server"</c> (case-insensitive).
    /// <see langword="null"/> or unrecognized uses the Engine default (<see cref="NodeRole.Peer"/>).
    /// </summary>
    public string? NodeRole { get; init; }

    /// <summary>
    /// The server endpoint a <see cref="Control.NodeRole.Client"/> instance forms its long-term MSMT
    /// connection to. Required when <see cref="NodeRole"/> is <c>"Client"</c>; unused otherwise.
    /// </summary>
    public UserEndpointConfig? ServerEndpoint { get; init; }

    /// <summary>
    /// Full server-user-map topology for a <see cref="Control.NodeRole.Server"/> instance. Keys are
    /// server user names (case-insensitive); describes every server in the cluster, including the
    /// local one. Required when <see cref="NodeRole"/> is <c>"Server"</c>; unused otherwise.
    /// </summary>
    public Dictionary<string, ServerUserConfigEntry> ServerUsers { get; init; } = [];

    /// <summary>Absolute directory containing the loaded config file, used to resolve relative certificate file paths. <see langword="null"/> when no config file was loaded.</summary>
    private string? ConfigDirectory { get; set; }

    /// <summary>
    /// Loads configuration from a file specified by the <c>--config</c> argument.
    /// Returns a default <see cref="EngineConfig"/> if the argument is absent. Whether this is called at all is decided by
    /// <see cref="IEngineController.ConfigFileEnabled"/>.
    /// </summary>
    public static EngineConfig Load(string[] args)
    {
        int idx = Array.IndexOf(args, "--config");
        if (idx >= 0 && idx + 1 < args.Length)
        {
            string configPath = args[idx + 1];
            string json = File.ReadAllText(configPath);
            EngineConfig config = JsonSerializer.Deserialize<EngineConfig>(json, jsonOptions) ?? new EngineConfig();
            config.ConfigDirectory = Path.GetDirectoryName(Path.GetFullPath(configPath));
            return config;
        }

        return new EngineConfig();
    }

    /// <summary>Resolves <see cref="PeerCertificateFile"/> against <see cref="ConfigDirectory"/> when it is a relative path.</summary>
    public string? GetPeerCertificateFilePath() => ResolveConfigRelativePath(PeerCertificateFile);

    /// <summary>Resolves <see cref="TrustedAuthorityCertificateFile"/> against <see cref="ConfigDirectory"/> when it is a relative path.</summary>
    public string? GetTrustedAuthorityCertificateFilePath() => ResolveConfigRelativePath(TrustedAuthorityCertificateFile);

    private string? ResolveConfigRelativePath(string? path)
        => path is null || ConfigDirectory is null || Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(ConfigDirectory, path));

    /// <summary>Returns the configured user entries as Engine model types, with case-insensitive key lookup.</summary>
    public IReadOnlyDictionary<string, UserEndpoint> GetUserEndpoints()
        => Users.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToEndpoint(),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>Parses <see cref="NodeRole"/>, defaulting to <see cref="Control.NodeRole.Peer"/> when unset or unrecognized.</summary>
    public Control.NodeRole GetNodeRole()
        => Enum.TryParse(NodeRole, ignoreCase: true, out Control.NodeRole role) ? role : Control.NodeRole.Peer;

    /// <summary>Returns the configured server user map as Engine model types, with case-insensitive key lookup.</summary>
    public IReadOnlyDictionary<string, ServerUserConfig> GetServerUsers()
        => ServerUsers.ToDictionary(
            kvp => kvp.Key,
            kvp => new ServerUserConfig
            {
                Endpoint = new UserEndpoint { IpAddress = kvp.Value.IpAddress, Port = kvp.Value.Port, SerialPort = kvp.Value.SerialPort, SerialAddress = kvp.Value.SerialAddress },
                ChildClients = kvp.Value.ChildClients
            },
            StringComparer.OrdinalIgnoreCase);
}

/// <summary>JSON deserialization shape for a user endpoint entry in the config file.</summary>
public sealed class UserEndpointConfig
{
    /// <summary>IPv4 or IPv6 address of the remote peer node. Ignored when <see cref="SerialPort"/> is set.</summary>
    public string IpAddress { get; init; } = string.Empty;
    /// <summary>TCP port of the remote peer node's peer server. Ignored when <see cref="SerialPort"/> is set.</summary>
    public int Port { get; init; }
    /// <summary>Name of the local MicroGate serial port cabled to the remote peer node; when set, this endpoint is reached over serial instead of IP.</summary>
    public string? SerialPort { get; init; }
    /// <summary>HDLC station address for the serial link. Defaults to 255 (0xFF). Both ends of the cable must use the same value.</summary>
    public byte SerialAddress { get; init; } = 0xFF;

    /// <summary>Converts this entry to the engine's endpoint model.</summary>
    public UserEndpoint ToEndpoint() => new() { IpAddress = IpAddress, Port = Port, SerialPort = SerialPort, SerialAddress = SerialAddress };
}

/// <summary>JSON deserialization shape for a server user map entry in the config file.</summary>
public sealed class ServerUserConfigEntry
{
    /// <summary>IPv4 or IPv6 address this server user listens on and other servers dial to reach it.</summary>
    public string IpAddress { get; init; } = string.Empty;
    /// <summary>TCP port this server user listens on and other servers dial to reach it.</summary>
    public int Port { get; init; }
    /// <summary>Name of the local MicroGate serial port through which this server user is reached; when set, IP address and port are ignored.</summary>
    public string? SerialPort { get; init; }
    /// <summary>HDLC station address for the serial link. Defaults to 255 (0xFF).</summary>
    public byte SerialAddress { get; init; } = 0xFF;
    /// <summary>Names of the client users that belong to this server.</summary>
    public List<string> ChildClients { get; init; } = [];
}
