namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Provides user group definitions, mapping group names to their members.</summary>
/// <remarks>Members may be user names or other group names, enabling nested group hierarchies.</remarks>
public interface IUserGroupProvider
{
    /// <summary>Returns all defined groups as a map of group name to member names (which may be user names or other group names).</summary>
    /// <param name="cancellation">Cancellation token.</param>
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetGroups(CancellationToken cancellation = default);
}

/// <summary>Implements <see cref="IUserGroupProvider"/> using group definitions from engine configuration.</summary>
internal sealed class UserGroupProvider : IUserGroupProvider
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _groups;

    /// <summary>Initializes a new instance loading group definitions from the given engine configuration.</summary>
    /// <param name="config">Engine configuration providing user group definitions.</param>
    public UserGroupProvider(EngineConfig config)
    {
        _groups = config.UserGroups.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value.AsReadOnly(),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetGroups(CancellationToken cancellation = default) =>
        Task.FromResult(_groups);
}
