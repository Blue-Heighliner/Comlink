namespace BlueHeighliner.Comlink.Engine;

/// <summary>Configuration loaded from a JSON file via the <c>--config</c> command-line argument.</summary>
public sealed class EngineConfig
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Run headless — as a normal peer client, with no GUI — instead of launching the desktop GUI.</summary>
    public bool HeadlessMode { get; init; }

    /// <summary>Debug site name override; skips <c>State.json</c> lookup when set.</summary>
    public string? SiteName { get; init; }

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
    /// <see langword="null"/> = auto (<c>SITE-{siteName}</c>); <c>"disable"</c> = no auth; explicit name = use that cert.
    /// </summary>
    public string? PeerCertificateName { get; init; }

    /// <summary>
    /// Site definitions and endpoint overrides. Keys are site names (case-insensitive).
    /// Entries may override an existing site's endpoint or introduce an entirely new site.
    /// </summary>
    public Dictionary<string, SiteEndpointConfig> Sites { get; init; } = [];

    /// <summary>
    /// Site group definitions. Keys are group names; values are lists of member names.
    /// Members may be site names or other group names, enabling nested hierarchies.
    /// </summary>
    public Dictionary<string, List<string>> SiteGroups { get; init; } = [];

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

    /// <summary>Returns the configured site entries as Engine model types, with case-insensitive key lookup.</summary>
    public IReadOnlyDictionary<string, SiteEndpoint> GetSiteEndpoints() =>
        Sites.ToDictionary(
            kvp => kvp.Key,
            kvp => new SiteEndpoint { IpAddress = kvp.Value.IpAddress, Port = kvp.Value.Port },
            StringComparer.OrdinalIgnoreCase);
}

/// <summary>JSON deserialization shape for a site endpoint entry in the config file.</summary>
public sealed class SiteEndpointConfig
{
    /// <summary>IPv4 or IPv6 address of the remote peer node.</summary>
    public string IpAddress { get; init; } = string.Empty;
    /// <summary>TCP port of the remote peer node's peer server.</summary>
    public int Port { get; init; }
}
