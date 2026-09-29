namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// A single named, colored security classification level. Levels are ordered: each level in
/// <see cref="IEngineController.SecurityLevels"/> outranks every one stated before it (see
/// <see cref="IEngineBuilder.SecurityLevels"/>).
/// </summary>
internal sealed record SecurityLevel
{
    /// <summary>Gets the display name of this level, and the value stored in a message's security level field and a user's assigned level.</summary>
    public required string Name { get; init; }
    /// <summary>Gets the hex color shown for this level in the top banner.</summary>
    public required string Color { get; init; }
}

/// <summary>Extension helpers for looking up rank and display information from an ordered set of <see cref="SecurityLevel"/> values.</summary>
internal static class SecurityLevelExtensions
{
    /// <summary>
    /// Returns the zero-based rank of <paramref name="levelName"/> within <paramref name="levels"/> (its index;
    /// higher means a more senior level), or <c>-1</c> if <paramref name="levelName"/> matches none of them.
    /// </summary>
    public static int GetRank(this IReadOnlyList<SecurityLevel> levels, string levelName)
    {
        for (int i = 0; i < levels.Count; i++)
        {
            if (string.Equals(levels[i].Name, levelName, StringComparison.OrdinalIgnoreCase)) { return i; }
        }
        return -1;
    }

    /// <summary>Returns the <see cref="SecurityLevel.Color"/> matching <paramref name="levelName"/>, or a neutral gray fallback if no level in <paramref name="levels"/> matches.</summary>
    public static string GetColor(this IReadOnlyList<SecurityLevel> levels, string levelName)
        => levels.FirstOrDefault(l => string.Equals(l.Name, levelName, StringComparison.OrdinalIgnoreCase))?.Color ?? "#5A5A5A";

    /// <summary>Returns whether <paramref name="levelName"/> matches one of <paramref name="levels"/> - <see langword="false"/> for an empty string or a name from a security level no longer configured.</summary>
    public static bool IsRecognized(this IReadOnlyList<SecurityLevel> levels, string levelName)
        => levels.GetRank(levelName) >= 0;
}
