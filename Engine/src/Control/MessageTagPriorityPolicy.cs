namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>
/// A single blocked message tag/priority combination. Either field may be left <see langword="null"/> to
/// match any value for that field, so a rule can block a specific tag regardless of priority, a specific
/// priority regardless of tag, or one specific tag/priority pair.
/// </summary>
public sealed record TagPriorityBlock
{
    /// <summary>The blocked tag (case-insensitive exact match), or <see langword="null"/> to match any tag.</summary>
    public string? Tag { get; init; }
    /// <summary>The blocked priority value, or <see langword="null"/> to match any priority.</summary>
    public int? Priority { get; init; }
}

/// <summary>Extension helpers for evaluating a set of <see cref="TagPriorityBlock"/> rules.</summary>
public static class TagPriorityBlockExtensions
{
    /// <summary>Returns <see langword="true"/> if any rule in <paramref name="blocks"/> matches the given tag/priority combination.</summary>
    /// <param name="blocks">The blocked combination rules to evaluate.</param>
    /// <param name="tag">The message tag to check.</param>
    /// <param name="priority">The message priority to check.</param>
    public static bool IsBlocked(this IReadOnlyList<TagPriorityBlock> blocks, string? tag, int priority) =>
        blocks.Any(b =>
            (b.Tag is null || string.Equals(b.Tag, tag, StringComparison.OrdinalIgnoreCase)) &&
            (b.Priority is null || b.Priority == priority));
}

/// <summary>Provides the set of blocked message tag/priority combinations, enforced when composing a draft.</summary>
public interface IMessageTagPriorityPolicy
{
    /// <summary>Returns every blocked tag/priority combination rule.</summary>
    IReadOnlyList<TagPriorityBlock> GetBlockedCombinations();
}

/// <summary>Default <see cref="IMessageTagPriorityPolicy"/> with no blocked combinations.</summary>
internal sealed class MessageTagPriorityPolicy : IMessageTagPriorityPolicy
{
    /// <inheritdoc />
    public IReadOnlyList<TagPriorityBlock> GetBlockedCombinations() => [];
}
