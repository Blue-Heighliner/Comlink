namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IMessageComposition"/> offering three priority levels instead of the Engine default's
/// one, renaming the tag input's label to <c>"Category"</c> (tags left enabled, matching the Engine
/// default — <c>config.json</c> can still override either field, applied separately at the Engine level;
/// see <c>Docs/Control.md</c>), and demonstrating both blocked-combination kinds: a tag blocked regardless
/// of priority, and a priority blocked regardless of tag.
/// </summary>
public sealed class SampleMessageComposition : DefaultMessageComposition
{
    private static readonly IReadOnlyList<MessagePriorityOption> Priorities =
    [
        new MessagePriorityOption { Name = "Low", Value = 0 },
        new MessagePriorityOption { Name = "Medium", Value = 1 },
        new MessagePriorityOption { Name = "High", Value = 2 }
    ];

    private static readonly IReadOnlyList<TagPriorityBlock> Blocks =
    [
        // Blocks the "SPAM" tag regardless of priority.
        new TagPriorityBlock { Tag = "SPAM", Priority = null },
        // Blocks High priority (value 2, see Priorities above) regardless of tag.
        new TagPriorityBlock { Tag = null, Priority = 2 }
    ];

    /// <inheritdoc />
    public override IReadOnlyList<MessagePriorityOption> GetPriorities() => Priorities;
    /// <inheritdoc />
    public override string TagLabel => "Category";
    /// <inheritdoc />
    public override IReadOnlyList<TagPriorityBlock> GetBlockedCombinations() => Blocks;
}
