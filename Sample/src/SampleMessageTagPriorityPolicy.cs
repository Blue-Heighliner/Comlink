namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IMessageTagPriorityPolicy"/> demonstrating both block kinds: a tag blocked regardless of
/// priority, and a priority blocked regardless of tag. Unlike Sample's other control interface overrides,
/// this one deliberately changes default behavior from the Engine's permissive "no blocks" default, since
/// that is the only way to usefully demonstrate this interface.
/// </summary>
public sealed class SampleMessageTagPriorityPolicy : IMessageTagPriorityPolicy
{
    private static readonly IReadOnlyList<TagPriorityBlock> Blocks =
    [
        // Blocks the "SPAM" tag regardless of priority.
        new TagPriorityBlock { Tag = "SPAM", Priority = null },
        // Blocks High priority (value 2, see SampleMessagePriorityProvider) regardless of tag.
        new TagPriorityBlock { Tag = null, Priority = 2 }
    ];

    /// <inheritdoc />
    public IReadOnlyList<TagPriorityBlock> GetBlockedCombinations() => Blocks;
}
