namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>A single selectable message priority level: a display name paired with its wire priority number.</summary>
public sealed record MessagePriorityOption
{
    /// <summary>Gets the display name shown to the user (e.g. in the draft editor's priority picker).</summary>
    public required string Name { get; init; }
    /// <summary>
    /// Gets the priority number stored via <see cref="IMessageFormat.SetPriority"/> and used verbatim as
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
    /// host changes its <see cref="IMessageComposition"/> registration and an older stored value no longer
    /// has a corresponding option.
    /// </summary>
    public static string GetLabel(this IReadOnlyList<MessagePriorityOption> priorities, int value) =>
        priorities.FirstOrDefault(p => p.Value == value)?.Name ?? value.ToString();
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
    public static bool IsBlocked(this IReadOnlyList<TagPriorityBlock> blocks, string? tag, int priority) =>
        blocks.Any(b =>
            (b.Tag is null || string.Equals(b.Tag, tag, StringComparison.OrdinalIgnoreCase)) &&
            (b.Priority is null || b.Priority == priority));
}

/// <summary>
/// Control interface for how messages are composed and displayed: the selectable priority levels, whether
/// message tags are shown anywhere in the UI and what the tag input is labeled, and which tag/priority
/// combinations are blocked outright when composing a draft.
/// </summary>
public interface IMessageComposition
{
    /// <summary>Returns every selectable priority level, in display order.</summary>
    IReadOnlyList<MessagePriorityOption> GetPriorities();
    /// <summary>
    /// When <see langword="true"/>, the draft editor shows a tag input and the entry listing shows each
    /// message's tag next to its priority. When <see langword="false"/>, tags are hidden everywhere in the
    /// UI — the underlying <see cref="IMessageFormat.GetTag"/>/<see cref="IMessageFormat.SetTag"/> values on
    /// existing messages are left untouched, just not surfaced.
    /// </summary>
    bool TagsEnabled { get; }
    /// <summary>
    /// The label used for the tag input's watermark in the draft editor. Lets a host call the concept
    /// something other than "Tag" (e.g. "Category", "Type") without changing engine behavior.
    /// </summary>
    string TagLabel { get; }
    /// <summary>Returns every blocked tag/priority combination rule, enforced when composing a draft.</summary>
    IReadOnlyList<TagPriorityBlock> GetBlockedCombinations();
}

/// <summary>
/// Implements <see cref="IMessageComposition"/> offering a single "Normal" priority level, tags enabled
/// with the label "Tag", and no blocked combinations. Hosts that need multiple selectable priority levels
/// or blocked combinations should override this registration. Describes non-config-file behavior; see
/// <see cref="ConfiguredMessageComposition"/> for how <c>config.json</c> overrides <see cref="TagsEnabled"/>/
/// <see cref="TagLabel"/>. Members are <see langword="virtual"/> so a host can inherit and override just one
/// — see <c>Docs/Control.md</c>.
/// </summary>
public class DefaultMessageComposition : IMessageComposition
{
    private static readonly IReadOnlyList<MessagePriorityOption> Priorities =
    [
        new MessagePriorityOption { Name = "Normal", Value = 0 }
    ];

    private static readonly IReadOnlyList<TagPriorityBlock> NoBlocks = [];

    /// <inheritdoc />
    public virtual IReadOnlyList<MessagePriorityOption> GetPriorities() => Priorities;
    /// <inheritdoc />
    public virtual bool TagsEnabled => true;
    /// <inheritdoc />
    public virtual string TagLabel => "Tag";
    /// <inheritdoc />
    public virtual IReadOnlyList<TagPriorityBlock> GetBlockedCombinations() => NoBlocks;
}

/// <summary>
/// Engine-level decorator applying <see cref="EngineConfig.MessageTagsEnabled"/>/<see cref="EngineConfig.MessageTagLabel"/>
/// over whichever <see cref="IMessageComposition"/> is registered (Engine default or a host override), when
/// set — <see cref="GetPriorities"/>/<see cref="GetBlockedCombinations"/> are left entirely to the wrapped
/// provider, since there is no corresponding <c>config.json</c> field for either. Registered by
/// <see cref="EngineExtensions.UseEngineConfigOverrides"/>, not by control-interface convention scanning.
/// </summary>
internal sealed class ConfiguredMessageComposition : IMessageComposition
{
    private readonly IMessageComposition _fallback;
    private readonly EngineConfig _config;

    /// <summary>Initializes a new instance wrapping <paramref name="fallback"/> with config overrides.</summary>
    /// <param name="fallback">The registered control-interface implementation to fall back to when config does not override.</param>
    /// <param name="config">Engine configuration providing the optional overrides.</param>
    public ConfiguredMessageComposition(IMessageComposition fallback, EngineConfig config)
    {
        _fallback = fallback;
        _config = config;
    }

    /// <inheritdoc />
    public IReadOnlyList<MessagePriorityOption> GetPriorities() => _fallback.GetPriorities();
    /// <inheritdoc />
    public bool TagsEnabled => _config.MessageTagsEnabled ?? _fallback.TagsEnabled;
    /// <inheritdoc />
    public string TagLabel => string.IsNullOrEmpty(_config.MessageTagLabel) ? _fallback.TagLabel : _config.MessageTagLabel;
    /// <inheritdoc />
    public IReadOnlyList<TagPriorityBlock> GetBlockedCombinations() => _fallback.GetBlockedCombinations();
}
