namespace BlueHeighliner.Comlink;

/// <summary>
/// A level a message can be sent at and a user can run at, shown as a foreground color on a background color. A level is the member of the host's level enum it was stated with: that member is its identity, in storage, in the network
/// configuration file and everywhere else. Its <see cref="Label"/> is only what the user interface shows. Levels are ordered: each level in <see cref="IEngineController.Levels"/> outranks every one stated before it
/// (see <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Level"/>).
/// </summary>
internal sealed record LevelDefinition
{
    /// <summary>Gets the text the user interface shows for this level. It identifies nothing.</summary>
    public required string Label { get; init; }
    /// <summary>Gets the member of the host's level enum this level is.</summary>
    public required Enum Key { get; init; }
    /// <summary>Gets the integer value of <see cref="Key"/>, which is how the level is stored in messages and drafts and so must never change for a member of the host's enum.</summary>
    public int Value => Convert.ToInt32(Key);
    /// <summary>Gets whether the GUI hides this level from a user composing a message; code may use any level.</summary>
    public bool IsHidden { get; init; }
    /// <summary>Gets the hex background color shown for this level: the top banner and the level's label in a message.</summary>
    public required string Background { get; init; }
    /// <summary>Gets the hex color of the text shown on <see cref="Background"/>.</summary>
    public string Foreground { get; init; } = "#FFFFFF";
}

/// <summary>Extension helpers for looking up rank and display information from an ordered set of <see cref="LevelDefinition"/> values, by the level's enum member.</summary>
internal static class LevelDefinitionExtensions
{
    /// <summary>
    /// Returns the zero-based rank of <paramref name="level"/> within <paramref name="levels"/> (its index; higher means a more senior level), or <c>-1</c> if <paramref name="level"/> is <see langword="null"/>
    /// or none of them.
    /// </summary>
    public static int GetRank(this IReadOnlyList<LevelDefinition> levels, Enum? level)
    {
        if (level is null)
        {
            return -1;
        }

        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i].Key.Equals(level))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Returns the level in <paramref name="levels"/> that is <paramref name="level"/>, or <see langword="null"/> for <see langword="null"/> or a level that is none of them.</summary>
    public static LevelDefinition? Find(this IReadOnlyList<LevelDefinition> levels, Enum? level) => level is null ? null : levels.FirstOrDefault(candidate => candidate.Key.Equals(level));

    /// <summary>Returns the level in <paramref name="levels"/> whose stored integer value is <paramref name="value"/>, or <see langword="null"/> for <see langword="null"/> or a value no longer configured.</summary>
    public static LevelDefinition? Find(this IReadOnlyList<LevelDefinition> levels, int? value) => value is null ? null : levels.FirstOrDefault(candidate => candidate.Value == value);

    /// <summary>Returns the label of <paramref name="level"/>, or an empty string for <see langword="null"/> or a level that is none of <paramref name="levels"/>.</summary>
    public static string LabelOf(this IReadOnlyList<LevelDefinition> levels, Enum? level) => levels.Find(level)?.Label ?? string.Empty;

    /// <summary>Returns the <see cref="LevelDefinition.Background"/> of <paramref name="level"/>, or a neutral gray fallback if it is none of <paramref name="levels"/>.</summary>
    public static string GetBackground(this IReadOnlyList<LevelDefinition> levels, Enum? level) => levels.Find(level)?.Background ?? "#5A5A5A";

    /// <summary>Returns the <see cref="LevelDefinition.Foreground"/> of <paramref name="level"/>, or white if it is none of <paramref name="levels"/>.</summary>
    public static string GetForeground(this IReadOnlyList<LevelDefinition> levels, Enum? level) => levels.Find(level)?.Foreground ?? "#FFFFFF";

    /// <summary>Returns whether <paramref name="level"/> is one of <paramref name="levels"/> - <see langword="false"/> for <see langword="null"/> or a level no longer configured.</summary>
    public static bool IsRecognized(this IReadOnlyList<LevelDefinition> levels, Enum? level) => levels.GetRank(level) >= 0;
}
