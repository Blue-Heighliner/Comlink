namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IUserGroupProvider"/> that reads group definitions from config, plus additional groups
/// defined via <c>GROUP_{NAME}</c> environment variables (comma-separated member list).
/// </summary>
public sealed class SampleUserGroupProvider : IUserGroupProvider
{
    private readonly EngineConfig _config;

    /// <summary>Initializes a new <see cref="SampleUserGroupProvider"/> using group definitions from the given config.</summary>
    /// <param name="config">Engine configuration providing group definitions.</param>
    public SampleUserGroupProvider(EngineConfig config) => _config = config;

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetGroups(CancellationToken cancellation = default)
    {
        Dictionary<string, IReadOnlyList<string>> groups = _config.UserGroups.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value.AsReadOnly(),
            StringComparer.OrdinalIgnoreCase);

        foreach (string key in Environment.GetEnvironmentVariables().Keys.Cast<string>())
        {
            if (!key.StartsWith("GROUP_", StringComparison.OrdinalIgnoreCase)) continue;
            string groupName = key["GROUP_".Length..].Replace("_", "-").ToUpperInvariant();
            string members = Environment.GetEnvironmentVariable(key) ?? string.Empty;
            groups[groupName] = members.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(m => m.ToUpperInvariant())
                .ToList();
        }

        return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(groups);
    }
}
