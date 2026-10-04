namespace BlueHeighliner.Comlink;

/// <summary>
/// A single blocked message tag/priority combination. Either field may be left <see langword="null"/> to
/// match any value for that field, so a rule can block a specific tag regardless of priority, a specific
/// priority regardless of tag, or one specific tag/priority pair.
/// </summary>
internal sealed record TagPriorityBlock
{
    /// <summary>The blocked tag (case-insensitive exact match), or <see langword="null"/> to match any tag.</summary>
    public string? Tag { get; init; }
    /// <summary>The blocked priority level, or <see langword="null"/> to match any priority.</summary>
    public Enum? Priority { get; init; }
}

/// <summary>Extension helpers for evaluating a set of <see cref="TagPriorityBlock"/> rules.</summary>
internal static class TagPriorityBlockExtensions
{
    /// <summary>Returns <see langword="true"/> if any rule in <paramref name="blocks"/> matches the given tag/priority combination.</summary>
    /// <param name="blocks">The blocked combination rules to evaluate.</param>
    /// <param name="tag">The message tag to check.</param>
    /// <param name="priority">The message priority to check.</param>
    public static bool IsBlocked(this IReadOnlyList<TagPriorityBlock> blocks, string? tag, Enum priority)
        => blocks.Any(b =>
            (b.Tag is null || string.Equals(b.Tag, tag, StringComparison.OrdinalIgnoreCase)) &&
            (b.Priority is null || b.Priority.Equals(priority)));
}
