namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>
/// The networking topology role a running instance takes on. See <c>Docs/Peer.md</c> for the full
/// description of each role's connection and routing behavior.
/// </summary>
public enum NodeRole
{
    /// <summary>Direct peer-to-peer networking: every user connects straight to every other user it addresses. The default.</summary>
    Peer,
    /// <summary>Hierarchical networking: all traffic flows through one long-term connection to a configured server (<see cref="INetworkTopology.GetServerEndpoint"/>).</summary>
    Client,
    /// <summary>Hierarchical networking: routes messages between its own child clients and other servers (<see cref="INetworkTopology.GetServerUsers"/>).</summary>
    Server
}

/// <summary>
/// Control interface for this instance's place in the peer/client/server networking topology: which
/// <see cref="NodeRole"/> it runs as, and — depending on that role — either the server it connects through
/// or the full server-user map it routes with. See <c>Docs/Peer.md#node-roles</c>.
/// </summary>
public interface INetworkTopology
{
    /// <summary>The configured role for this instance.</summary>
    NodeRole Role { get; }
    /// <summary>
    /// Returns the server endpoint a <see cref="NodeRole.Client"/> instance forms its single long-term
    /// connection to, or <see langword="null"/> if none is configured. Unused outside <see cref="NodeRole.Client"/>.
    /// </summary>
    UserEndpoint? GetServerEndpoint();
    /// <summary>
    /// Returns the full server-user map a <see cref="NodeRole.Server"/> instance routes with, keyed by
    /// server user name (case-insensitive) — every server in the cluster, not just the local one. Unused
    /// outside <see cref="NodeRole.Server"/>.
    /// </summary>
    /// <param name="cancellation">Cancellation token.</param>
    Task<IReadOnlyDictionary<string, ServerUserConfig>> GetServerUsers(CancellationToken cancellation = default);
}

/// <summary>
/// Implements <see cref="INetworkTopology"/> as a plain <see cref="NodeRole.Peer"/> with no server endpoint
/// or server users configured. Describes non-config-file behavior; see <see cref="ConfiguredNetworkTopology"/>
/// for how <c>config.json</c> overrides this. Members are <see langword="virtual"/> so a host can inherit
/// and override just one — see <c>Docs/Control.md</c>.
/// </summary>
public class DefaultNetworkTopology : INetworkTopology
{
    private static readonly IReadOnlyDictionary<string, ServerUserConfig> EmptyServerUsers =
        new Dictionary<string, ServerUserConfig>();

    /// <inheritdoc />
    public virtual NodeRole Role => NodeRole.Peer;
    /// <inheritdoc />
    public virtual UserEndpoint? GetServerEndpoint() => null;
    /// <inheritdoc />
    public virtual Task<IReadOnlyDictionary<string, ServerUserConfig>> GetServerUsers(CancellationToken cancellation = default) =>
        Task.FromResult(EmptyServerUsers);
}

/// <summary>
/// Engine-level decorator applying <see cref="EngineConfig.NodeRole"/>/<see cref="EngineConfig.ServerEndpoint"/>/
/// <see cref="EngineConfig.ServerUsers"/> over whichever <see cref="INetworkTopology"/> is registered (Engine
/// default or a host override). Registered by <see cref="EngineExtensions.UseEngineConfigOverrides"/>, not by
/// control-interface convention scanning. Note: the concrete <see cref="Peer.IPeerService"/> implementation is
/// selected separately, directly from <see cref="EngineConfig.NodeRole"/>, at DI composition time in
/// <see cref="EngineExtensions.UseEngine"/> — before this decorator (or any other DI-resolved service) exists
/// to consult.
/// </summary>
internal sealed class ConfiguredNetworkTopology : INetworkTopology
{
    private readonly INetworkTopology _fallback;
    private readonly EngineConfig _config;

    /// <summary>Initializes a new instance wrapping <paramref name="fallback"/> with config overrides.</summary>
    /// <param name="fallback">The registered control-interface implementation to fall back to when config does not override.</param>
    /// <param name="config">Engine configuration providing the optional overrides.</param>
    public ConfiguredNetworkTopology(INetworkTopology fallback, EngineConfig config)
    {
        _fallback = fallback;
        _config = config;
    }

    /// <inheritdoc />
    public NodeRole Role => _config.NodeRole is not null && Enum.TryParse(_config.NodeRole, ignoreCase: true, out NodeRole role)
        ? role
        : _fallback.Role;

    /// <inheritdoc />
    public UserEndpoint? GetServerEndpoint() =>
        _config.ServerEndpoint is { } endpoint
            ? new UserEndpoint { IpAddress = endpoint.IpAddress, Port = endpoint.Port }
            : _fallback.GetServerEndpoint();

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, ServerUserConfig>> GetServerUsers(CancellationToken cancellation = default)
    {
        IReadOnlyDictionary<string, ServerUserConfig> fallbackServers = await _fallback.GetServerUsers(cancellation);
        Dictionary<string, ServerUserConfig> merged = new(fallbackServers, StringComparer.OrdinalIgnoreCase);
        foreach ((string serverName, ServerUserConfig serverConfig) in _config.GetServerUsers())
            merged[serverName] = serverConfig;
        return merged;
    }
}
