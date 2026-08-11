namespace BlueHeighliner.Comlink.Engine.Models;

/// <summary>Identity and environment metadata for a user.</summary>
public sealed class UserInfo
{
    /// <summary>Canonical name of the user.</summary>
    public required string Name { get; init; }
    /// <summary>Short alphanumeric code identifying the user.</summary>
    public required string Code { get; init; }
    /// <summary>Human-readable environment label (e.g. "Production").</summary>
    public required string EnvironmentTitle { get; init; }
    /// <summary>Hex color string associated with this environment.</summary>
    public required string EnvironmentColor { get; init; }
    /// <summary>Names of the groups this user is a member of.</summary>
    public IReadOnlyList<string> Groups { get; init; } = [];
}
