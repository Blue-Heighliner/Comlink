namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Control interface: returns all known site names in the messaging system.</summary>
public interface ISiteNameDirectory
{
    /// <summary>Returns all known site and group names in the messaging system.</summary>
    /// <param name="cancellation">Cancellation token.</param>
    Task<IReadOnlyList<string>> GetAllSiteNames(CancellationToken cancellation = default);
}

/// <summary>Implements <see cref="ISiteNameDirectory"/> returning site and group names from engine configuration.</summary>
internal sealed class SiteNameDirectory : ISiteNameDirectory
{
    private readonly IReadOnlyList<string> _names;

    /// <summary>Initializes a new instance loading site and group names from the given engine configuration.</summary>
    /// <param name="config">Engine configuration providing site and group definitions.</param>
    public SiteNameDirectory(EngineConfig config)
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (string site in config.Sites.Keys)
            names.Add(site.ToUpperInvariant());
        foreach (string group in config.SiteGroups.Keys)
            names.Add(group.ToUpperInvariant());
        _names = [.. names.OrderBy(n => n)];
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetAllSiteNames(CancellationToken cancellation = default) =>
        Task.FromResult(_names);
}
