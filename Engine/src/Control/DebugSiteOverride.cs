namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Debug override that supplies a fixed site name, bypassing the normal <c>State.json</c> lookup.</summary>
public interface IDebugSiteOverride
{
    /// <summary>The overridden site name, or <see langword="null"/> if no override is active.</summary>
    string? SiteName { get; }
}

/// <summary>Implements <see cref="IDebugSiteOverride"/> using the site name from engine configuration.</summary>
internal sealed class DebugSiteOverride : IDebugSiteOverride
{
    private readonly EngineConfig _config;

    /// <summary>Initializes a new instance with the given engine configuration.</summary>
    /// <param name="config">Engine configuration providing the optional site name override.</param>
    public DebugSiteOverride(EngineConfig config) => _config = config;

    /// <inheritdoc />
    public string? SiteName => _config.SiteName;
}
