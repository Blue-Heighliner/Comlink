namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>A single selectable message priority level: a display name paired with its wire priority number.</summary>
public sealed record MessagePriorityOption
{
    /// <summary>Gets the display name shown to the user (e.g. in the draft editor's priority picker).</summary>
    public required string Name { get; init; }
    /// <summary>
    /// Gets the priority number stored via <see cref="IEngineController.SetPriority"/> and used verbatim as
    /// the OFT send priority (larger values are sent first — see <c>Docs/Peer.md</c>).
    /// </summary>
    public required int Value { get; init; }
}

/// <summary>Extension helpers for looking up display information from a set of <see cref="MessagePriorityOption"/> values.</summary>
public static class MessagePriorityOptionExtensions
{
    /// <summary>
    /// Returns the display <see cref="MessagePriorityOption.Name"/> matching <paramref name="value"/>, or the
    /// plain numeric value as a string if no option in <paramref name="priorities"/> matches — e.g. after a
    /// host changes its <see cref="IEngineController.Priorities"/> registration and an older stored value
    /// no longer has a corresponding option.
    /// </summary>
    public static string GetLabel(this IReadOnlyList<MessagePriorityOption> priorities, int value)
        => priorities.FirstOrDefault(p => p.Value == value)?.Name ?? value.ToString();
}

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
    public static bool IsBlocked(this IReadOnlyList<TagPriorityBlock> blocks, string? tag, int priority)
        => blocks.Any(b =>
            (b.Tag is null || string.Equals(b.Tag, tag, StringComparison.OrdinalIgnoreCase)) &&
            (b.Priority is null || b.Priority == priority));
}
