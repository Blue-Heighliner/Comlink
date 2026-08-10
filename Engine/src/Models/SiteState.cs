namespace BlueHeighliner.Comlink.Engine.Models;

/// <summary>Mutable snapshot of the local site's installation state read from persistent storage.</summary>
public sealed class SiteState
{
    /// <summary>Canonical site name, or <c>null</c> if not yet installed.</summary>
    public string? SiteName { get; set; }
    /// <summary>Short alphanumeric site code, or <c>null</c> if not yet installed.</summary>
    public string? SiteCode { get; set; }
    /// <summary>Human-readable environment label, or <c>null</c> if not yet installed.</summary>
    public string? EnvironmentTitle { get; set; }
    /// <summary>Hex color string for this environment, or <c>null</c> if not yet installed.</summary>
    public string? EnvironmentColor { get; set; }

    /// <summary>Returns <c>true</c> when the site has been installed (i.e. <see cref="SiteName"/> is set).</summary>
    public bool IsInstalled => SiteName != null;
}
