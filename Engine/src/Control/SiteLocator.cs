namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Control interface: resolves a site name to its TCP peer endpoint for P2P delivery.</summary>
public interface ISiteLocator
{
    /// <summary>Returns the TCP endpoint for <paramref name="siteName"/>, or <see langword="null"/> if the site is unknown.</summary>
    /// <param name="siteName">The site name to resolve.</param>
    /// <param name="cancellation">Cancellation token.</param>
    Task<SiteEndpoint?> GetEndpoint(string siteName, CancellationToken cancellation = default);
}

/// <summary>Implements <see cref="ISiteLocator"/> resolving endpoints from site definitions in engine configuration.</summary>
internal sealed class SiteLocator : ISiteLocator
{
    private readonly IReadOnlyDictionary<string, SiteEndpoint> _endpoints;

    /// <summary>Initializes a new instance loading site endpoints from the given engine configuration.</summary>
    /// <param name="config">Engine configuration providing site endpoint definitions.</param>
    public SiteLocator(EngineConfig config) => _endpoints = config.GetSiteEndpoints();

    /// <inheritdoc />
    public Task<SiteEndpoint?> GetEndpoint(string siteName, CancellationToken cancellation = default)
    {
        _endpoints.TryGetValue(siteName, out SiteEndpoint? endpoint);
        return Task.FromResult(endpoint);
    }
}
