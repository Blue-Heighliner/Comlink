namespace BlueHeighliner.Comlink;

/// <summary>
/// A single named, colored security classification level. Levels are ordered: each level in
/// <see cref="IEngineController.MessageLevels"/> outranks every one stated before it (see
/// <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.MessageLevels"/>).
/// </summary>
internal sealed record MessageLevel
{
    /// <summary>Gets the display name of this level, and the value stored in a message's level field and a user's assigned level.</summary>
    public required string Name { get; init; }
    /// <summary>Gets the member of the host's message level enum this level is.</summary>
    public Enum? Key { get; init; }
    /// <summary>Gets the integer value of <see cref="Key"/>, which is how the level is stored in drafts and so must never change for a member of the host's enum, or <see langword="null"/> without a key.</summary>
    public int? Value => Key is null ? null : Convert.ToInt32(Key);
    /// <summary>Gets the hex color shown for this level in the top banner.</summary>
    public required string Color { get; init; }
}

/// <summary>Extension helpers for looking up rank and display information from an ordered set of <see cref="MessageLevel"/> values.</summary>
internal static class MessageLevelExtensions
{
    /// <summary>
    /// Returns the zero-based rank of <paramref name="levelName"/> within <paramref name="levels"/> (its index;
    /// higher means a more senior level), or <c>-1</c> if <paramref name="levelName"/> matches none of them.
    /// </summary>
    public static int GetRank(this IReadOnlyList<MessageLevel> levels, string levelName)
    {
        for (int i = 0; i < levels.Count; i++)
        {
            if (string.Equals(levels[i].Name, levelName, StringComparison.OrdinalIgnoreCase)) { return i; }
        }
        return -1;
    }

    /// <summary>Returns the <see cref="MessageLevel.Color"/> matching <paramref name="levelName"/>, or a neutral gray fallback if no level in <paramref name="levels"/> matches.</summary>
    public static string GetColor(this IReadOnlyList<MessageLevel> levels, string levelName)
        => levels.FirstOrDefault(l => string.Equals(l.Name, levelName, StringComparison.OrdinalIgnoreCase))?.Color ?? "#5A5A5A";

    /// <summary>Returns whether <paramref name="levelName"/> matches one of <paramref name="levels"/> - <see langword="false"/> for an empty string or a name from a message level no longer configured.</summary>
    public static bool IsRecognized(this IReadOnlyList<MessageLevel> levels, string levelName)
        => levels.GetRank(levelName) >= 0;
}
