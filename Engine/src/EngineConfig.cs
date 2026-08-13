namespace BlueHeighliner.Comlink.Engine;

/// <summary>Configuration loaded from a JSON file via the <c>--config</c> command-line argument.</summary>
public sealed class EngineConfig
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

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
    /// TLS certificate subject name for peer auth.
    /// <see langword="null"/> = auto (<c>USER-{userName}</c>); <c>"disable"</c> = no auth; explicit name = use that cert.
    /// </summary>
    public string? PeerCertificateName { get; init; }

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
    /// Loads configuration from a file specified by the <c>--config</c> argument.
    /// Returns a default <see cref="EngineConfig"/> if the argument is absent or config loading is not enabled for this build.
    /// </summary>
    public static EngineConfig Load(string[] args)
    {
#if DEBUG || ALLOW_CONFIG
        int idx = Array.IndexOf(args, "--config");
        if (idx >= 0 && idx + 1 < args.Length)
        {
            string json = File.ReadAllText(args[idx + 1]);
            return JsonSerializer.Deserialize<EngineConfig>(json, _jsonOptions) ?? new EngineConfig();
        }
#endif
        return new EngineConfig();
    }

    /// <summary>Returns the configured user entries as Engine model types, with case-insensitive key lookup.</summary>
    public IReadOnlyDictionary<string, UserEndpoint> GetUserEndpoints() =>
        Users.ToDictionary(
            kvp => kvp.Key,
            kvp => new UserEndpoint { IpAddress = kvp.Value.IpAddress, Port = kvp.Value.Port },
            StringComparer.OrdinalIgnoreCase);
}

/// <summary>JSON deserialization shape for a user endpoint entry in the config file.</summary>
public sealed class UserEndpointConfig
{
    /// <summary>IPv4 or IPv6 address of the remote peer node.</summary>
    public string IpAddress { get; init; } = string.Empty;
    /// <summary>TCP port of the remote peer node's peer server.</summary>
    public int Port { get; init; }
}
