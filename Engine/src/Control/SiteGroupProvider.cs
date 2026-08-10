namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Provides site group definitions, mapping group names to their members.</summary>
/// <remarks>Members may be site names or other group names, enabling nested group hierarchies.</remarks>
public interface ISiteGroupProvider
{
    /// <summary>Returns all defined groups as a map of group name to member names (which may be site names or other group names).</summary>
    /// <param name="cancellation">Cancellation token.</param>
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetGroups(CancellationToken cancellation = default);
}

/// <summary>Implements <see cref="ISiteGroupProvider"/> using group definitions from engine configuration.</summary>
internal sealed class SiteGroupProvider : ISiteGroupProvider
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _groups;

    /// <summary>Initializes a new instance loading group definitions from the given engine configuration.</summary>
    /// <param name="config">Engine configuration providing site group definitions.</param>
    public SiteGroupProvider(EngineConfig config)
    {
        _groups = config.SiteGroups.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value.AsReadOnly(),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetGroups(CancellationToken cancellation = default) =>
        Task.FromResult(_groups);
}
