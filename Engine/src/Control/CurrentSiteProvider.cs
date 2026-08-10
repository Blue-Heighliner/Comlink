namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Exposes the mutable site name of the currently running instance.</summary>
public interface ICurrentSiteProvider
{
    /// <summary>The site name of the currently registered instance, or <see langword="null"/> if not yet installed.</summary>
    string? SiteName { get; set; }
}

/// <summary>Tracks the site name of the currently running instance.</summary>
public sealed class CurrentSiteProvider : ICurrentSiteProvider
{
    /// <inheritdoc />
    public string? SiteName { get; set; }
}
