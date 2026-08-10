namespace BlueHeighliner.Comlink.Sample;

/// <summary>Sample <see cref="ISiteLocator"/> that checks config overrides first, then falls back to <c>PEER_{SITENAME}=ip:port</c> environment variables.</summary>
internal sealed class SampleSiteLocator : ISiteLocator
{
    private readonly IReadOnlyDictionary<string, SiteEndpoint> _overrides;

    /// <summary>Initializes a new <see cref="SampleSiteLocator"/> using site definitions from the given config.</summary>
    /// <param name="config">Engine configuration providing site endpoint overrides and definitions.</param>
    public SampleSiteLocator(EngineConfig config)
        => _overrides = config.GetSiteEndpoints();

    /// <inheritdoc />
    public Task<SiteEndpoint?> GetEndpoint(string siteName, CancellationToken cancellation = default)
    {
        if (_overrides.TryGetValue(siteName, out SiteEndpoint? endpoint))
            return Task.FromResult<SiteEndpoint?>(endpoint);

        string envKey = $"PEER_{siteName.ToUpperInvariant().Replace("-", "_")}";
        string? value = Environment.GetEnvironmentVariable(envKey);

        if (value is null) return Task.FromResult<SiteEndpoint?>(null);

        string[] parts = value.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out int port))
            return Task.FromResult<SiteEndpoint?>(null);

        return Task.FromResult<SiteEndpoint?>(new SiteEndpoint { IpAddress = parts[0], Port = port });
    }
}
