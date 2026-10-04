namespace BlueHeighliner.Comlink;

/// <summary>Implements <see cref="IPriorityBuilder{TPriority}"/> and <see cref="IPriorityLevelBuilder{TPriority}"/>, recording what is stated about each level and turning it into the engine's priority options.</summary>
internal sealed class PriorityBuilder<TPriority> : IPriorityLevelBuilder<TPriority> where TPriority : struct, Enum
{
    private readonly Dictionary<TPriority, (string? Label, PriorityMode? Mode)> options = [];
    private readonly List<TagPriorityBlock> blocks = [];
    private TPriority current;

    /// <summary>The blocked tag and priority combinations that were stated.</summary>
    public IReadOnlyList<TagPriorityBlock> Blocks => blocks;

    /// <inheritdoc />
    public IPriorityLevelBuilder<TPriority> Priority(TPriority priority)
    {
        current = priority;
        return this;
    }

    /// <inheritdoc />
    public IPriorityLevelBuilder<TPriority> Label(string label)
    {
        options[current] = (label, options.GetValueOrDefault(current).Mode);
        return this;
    }

    /// <inheritdoc />
    public IPriorityLevelBuilder<TPriority> Mode(PriorityMode mode)
    {
        options[current] = (options.GetValueOrDefault(current).Label, mode);
        return this;
    }

    /// <inheritdoc />
    public IPriorityBuilder<TPriority> Block(string? tag, TPriority? priority)
    {
        blocks.Add(new TagPriorityBlock { Tag = tag, Priority = priority });
        return this;
    }

    /// <summary>Builds the priority options, one per member of the enum, in declaration order.</summary>
    public List<MessagePriorityOption> Build()
        => [.. Enum.GetValues<TPriority>().Select((priority, index) =>
        {
            (string? label, PriorityMode? mode) = options.GetValueOrDefault(priority);
            return new MessagePriorityOption { Name = label ?? priority.ToString().ToUpperInvariant(), Value = index, Mode = mode ?? PriorityMode.User, Key = priority };
        })];
}
