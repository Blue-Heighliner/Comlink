namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample ISiteCodeResolver that reads site info from environment variables.
/// Set SITE_{CODE}_NAME, SITE_{CODE}_ENV_TITLE, SITE_{CODE}_ENV_COLOR to define a site.
/// </summary>
public sealed class SampleSiteCodeResolver : ISiteCodeResolver
{
    private static readonly Dictionary<string, SiteInfo> _builtIn = new()
    {
        ["CODE1"] = new SiteInfo { Name = "TEST1", Code = "CODE1", EnvironmentTitle = "DEV", EnvironmentColor = "#1565C0" },
        ["CODE2"] = new SiteInfo { Name = "TEST2", Code = "CODE2", EnvironmentTitle = "DEV", EnvironmentColor = "#1565C0" },
        ["CODE3"] = new SiteInfo { Name = "TEST3", Code = "CODE3", EnvironmentTitle = "DEV", EnvironmentColor = "#1565C0" }
    };

    /// <inheritdoc />
    public Task<SiteInfo?> Resolve(string siteCode, CancellationToken cancellation = default)
    {
        string upper = siteCode.ToUpperInvariant();
        string prefix = $"SITE_{upper}_";
        string? name = Environment.GetEnvironmentVariable($"{prefix}NAME");

        if (name is not null)
        {
            return Task.FromResult<SiteInfo?>(new SiteInfo
            {
                Name = name,
                Code = siteCode,
                EnvironmentTitle = Environment.GetEnvironmentVariable($"{prefix}ENV_TITLE") ?? "Development",
                EnvironmentColor = Environment.GetEnvironmentVariable($"{prefix}ENV_COLOR") ?? "#1565C0"
            });
        }

        _builtIn.TryGetValue(upper, out SiteInfo? builtIn);
        return Task.FromResult(builtIn);
    }
}
