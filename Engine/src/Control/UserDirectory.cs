namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>
/// Control interface for everything the engine knows about addressable users and groups: resolving a user
/// name to its peer endpoint for delivery, group membership for address expansion, and the full list of
/// known user/group names for the destination auto-complete.
/// </summary>
/// <remarks>Group members may be user names or other group names, enabling nested group hierarchies.</remarks>
public interface IUserDirectory
{
    /// <summary>Returns the TCP endpoint for <paramref name="userName"/>, or <see langword="null"/> if the user is unknown.</summary>
    /// <param name="userName">The user name to resolve.</param>
    /// <param name="cancellation">Cancellation token.</param>
    Task<UserEndpoint?> GetEndpoint(string userName, CancellationToken cancellation = default);
    /// <summary>Returns all defined groups as a map of group name to member names (which may be user names or other group names).</summary>
    /// <param name="cancellation">Cancellation token.</param>
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetGroups(CancellationToken cancellation = default);
    /// <summary>Returns all known user and group names in the messaging system.</summary>
    /// <param name="cancellation">Cancellation token.</param>
    Task<IReadOnlyList<string>> GetAllUserNames(CancellationToken cancellation = default);
}

/// <summary>
/// Implements <see cref="IUserDirectory"/> with no known users, groups, or names. Describes non-config-file
/// behavior; see <see cref="ConfiguredUserDirectory"/> for how <c>config.json</c> overrides this. Members
/// are <see langword="virtual"/> so a host can inherit and override just one — e.g. to add its own names
/// while still calling <c>base.GetAllUserNames(cancellation)</c> — see <c>Docs/Control.md</c>.
/// </summary>
public class DefaultUserDirectory : IUserDirectory
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptyGroups =
        new Dictionary<string, IReadOnlyList<string>>();
    private static readonly IReadOnlyList<string> EmptyNames = [];

    /// <inheritdoc />
    public virtual Task<UserEndpoint?> GetEndpoint(string userName, CancellationToken cancellation = default) =>
        Task.FromResult<UserEndpoint?>(null);

    /// <inheritdoc />
    public virtual Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetGroups(CancellationToken cancellation = default) =>
        Task.FromResult(EmptyGroups);

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<string>> GetAllUserNames(CancellationToken cancellation = default) =>
        Task.FromResult(EmptyNames);
}

/// <summary>
/// Engine-level decorator applying <see cref="EngineConfig.Users"/>/<see cref="EngineConfig.UserGroups"/>
/// over whichever <see cref="IUserDirectory"/> is registered (Engine default or a host override):
/// <see cref="GetEndpoint"/> resolves configured users first, falling back to the wrapped provider;
/// <see cref="GetGroups"/> merges configured groups over the wrapped provider's own, config winning on key
/// conflicts; <see cref="GetAllUserNames"/> unions the wrapped provider's names with configured user and
/// group names. Registered by <see cref="EngineExtensions.UseEngineConfigOverrides"/>, not by
/// control-interface convention scanning.
/// </summary>
internal sealed class ConfiguredUserDirectory : IUserDirectory
{
    private readonly IUserDirectory _fallback;
    private readonly EngineConfig _config;
    private readonly IReadOnlyDictionary<string, UserEndpoint> _endpoints;

    /// <summary>Initializes a new instance wrapping <paramref name="fallback"/> with config overrides.</summary>
    /// <param name="fallback">The registered control-interface implementation to fall back to when config does not override.</param>
    /// <param name="config">Engine configuration providing user, group, and endpoint definitions.</param>
    public ConfiguredUserDirectory(IUserDirectory fallback, EngineConfig config)
    {
        _fallback = fallback;
        _config = config;
        _endpoints = config.GetUserEndpoints();
    }

    /// <inheritdoc />
    public Task<UserEndpoint?> GetEndpoint(string userName, CancellationToken cancellation = default) =>
        _endpoints.TryGetValue(userName, out UserEndpoint? endpoint)
            ? Task.FromResult<UserEndpoint?>(endpoint)
            : _fallback.GetEndpoint(userName, cancellation);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetGroups(CancellationToken cancellation = default)
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> fallbackGroups = await _fallback.GetGroups(cancellation);
        Dictionary<string, IReadOnlyList<string>> merged = new(fallbackGroups, StringComparer.OrdinalIgnoreCase);
        foreach ((string groupName, List<string> members) in _config.UserGroups)
            merged[groupName] = members.AsReadOnly();
        return merged;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAllUserNames(CancellationToken cancellation = default)
    {
        HashSet<string> names = new(await _fallback.GetAllUserNames(cancellation), StringComparer.OrdinalIgnoreCase);
        foreach (string user in _config.Users.Keys)
            names.Add(user.ToUpperInvariant());
        foreach (string group in _config.UserGroups.Keys)
            names.Add(group.ToUpperInvariant());
        return [.. names.OrderBy(n => n)];
    }
}
