namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="ISiteNameDirectory"/> that returns built-in site names, sites and groups defined in config,
/// and sites discovered via <c>PEER_*</c> environment variables.
/// </summary>
public sealed class SampleSiteNameDirectory : ISiteNameDirectory
{
    private static readonly string[] _builtIn = ["TEST1", "TEST2", "TEST3"];

    private readonly EngineConfig _config;
    private readonly ISiteGroupProvider _groupProvider;

    /// <summary>Initializes a new <see cref="SampleSiteNameDirectory"/> with config and group provider.</summary>
    /// <param name="config">Engine configuration providing additional site and group definitions.</param>
    /// <param name="groupProvider">Provides group definitions whose names are addressable destinations.</param>
    public SampleSiteNameDirectory(EngineConfig config, ISiteGroupProvider groupProvider)
    {
        _config = config;
        _groupProvider = groupProvider;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAllSiteNames(CancellationToken cancellation = default)
    {
        HashSet<string> names = new(_builtIn, StringComparer.OrdinalIgnoreCase);

        foreach (string siteName in _config.Sites.Keys)
            names.Add(siteName.ToUpperInvariant());

        foreach (string key in Environment.GetEnvironmentVariables().Keys.Cast<string>())
        {
            if (key.StartsWith("PEER_", StringComparison.OrdinalIgnoreCase))
            {
                string siteName = key["PEER_".Length..].Replace("_", "-");
                names.Add(siteName.ToUpperInvariant());
            }
        }

        IReadOnlyDictionary<string, IReadOnlyList<string>> groups = await _groupProvider.GetGroups(cancellation);
        foreach (string groupName in groups.Keys)
            names.Add(groupName.ToUpperInvariant());

        return [.. names.OrderBy(x => x)];
    }
}
