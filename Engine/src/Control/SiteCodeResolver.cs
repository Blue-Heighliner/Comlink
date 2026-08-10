namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Control interface: resolves a site code to its SiteInfo. Returns null when the code is unrecognized.</summary>
public interface ISiteCodeResolver
{
    /// <summary>Resolves <paramref name="siteCode"/> to its <see cref="SiteInfo"/>, or <see langword="null"/> if the code is unrecognized.</summary>
    /// <param name="siteCode">The site installation code to resolve.</param>
    /// <param name="cancellation">Cancellation token.</param>
    Task<SiteInfo?> Resolve(string siteCode, CancellationToken cancellation = default);
}

/// <summary>Stub <see cref="ISiteCodeResolver"/> that only recognizes the hard-coded test code "CODE".</summary>
internal sealed class SiteCodeResolver : ISiteCodeResolver
{
    /// <inheritdoc />
    public Task<SiteInfo?> Resolve(string siteCode, CancellationToken cancellation = default)
    {
        SiteInfo? result = siteCode.Equals("CODE", StringComparison.OrdinalIgnoreCase)
            ? new SiteInfo { Name = "TEST", Code = "CODE", EnvironmentTitle = "Test", EnvironmentColor = "#888888" }
            : null;
        return Task.FromResult(result);
    }
}
