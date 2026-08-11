namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IUserNameDirectory"/> that returns built-in user names, users and groups defined in config,
/// and users discovered via <c>PEER_*</c> environment variables.
/// </summary>
public sealed class SampleUserNameDirectory : IUserNameDirectory
{
    private static readonly string[] _builtIn = ["TEST1", "TEST2", "TEST3"];

    private readonly EngineConfig _config;
    private readonly IUserGroupProvider _groupProvider;

    /// <summary>Initializes a new <see cref="SampleUserNameDirectory"/> with config and group provider.</summary>
    /// <param name="config">Engine configuration providing additional user and group definitions.</param>
    /// <param name="groupProvider">Provides group definitions whose names are addressable destinations.</param>
    public SampleUserNameDirectory(EngineConfig config, IUserGroupProvider groupProvider)
    {
        _config = config;
        _groupProvider = groupProvider;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAllUserNames(CancellationToken cancellation = default)
    {
        HashSet<string> names = new(_builtIn, StringComparer.OrdinalIgnoreCase);

        foreach (string userName in _config.Users.Keys)
            names.Add(userName.ToUpperInvariant());

        foreach (string key in Environment.GetEnvironmentVariables().Keys.Cast<string>())
        {
            if (key.StartsWith("PEER_", StringComparison.OrdinalIgnoreCase))
            {
                string userName = key["PEER_".Length..].Replace("_", "-");
                names.Add(userName.ToUpperInvariant());
            }
        }

        IReadOnlyDictionary<string, IReadOnlyList<string>> groups = await _groupProvider.GetGroups(cancellation);
        foreach (string groupName in groups.Keys)
            names.Add(groupName.ToUpperInvariant());

        return [.. names.OrderBy(x => x)];
    }
}
