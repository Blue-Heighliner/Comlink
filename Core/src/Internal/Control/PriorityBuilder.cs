namespace BlueHeighliner.Comlink;

/// <summary>Collects what is stated through <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}"/> and <see cref="IPriorityLevelBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}"/>, recording what is stated about each level and turning it into the engine's priority options.</summary>
internal sealed class PriorityBuilder<TPriority> where TPriority : struct, Enum
{
    private readonly Dictionary<TPriority, (string? Label, bool IsHidden)> options = [];
    private readonly List<TPriority> order = [];
    private TPriority current;

    /// <inheritdoc cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Priority(TPriority)"/>
    public PriorityBuilder<TPriority> Priority(TPriority priority)
    {
        current = priority;
        if (!order.Contains(priority))
        {
            order.Add(priority);
        }
        return this;
    }

    /// <inheritdoc cref="IPriorityLevelBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Label"/>
    public PriorityBuilder<TPriority> Label(string label)
    {
        options[current] = (label, options.GetValueOrDefault(current).IsHidden);
        return this;
    }

    /// <inheritdoc cref="IPriorityLevelBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.IsHidden"/>
    public PriorityBuilder<TPriority> IsHidden(bool isHidden)
    {
        options[current] = (options.GetValueOrDefault(current).Label, isHidden);
        return this;
    }

    /// <summary>Builds the priority options, one per stated member, in the order stated.</summary>
    /// <exception cref="InvalidOperationException">A level's value does not fit an <see cref="int"/>, which is how it is stored, or two levels have the same name.</exception>
    public List<MessagePriorityOption> Build()
    {
        List<MessagePriorityOption> built =
        [.. order.Select((priority, index) =>
        {
            (string? label, bool isHidden) = options.GetValueOrDefault(priority);
            if (Convert.ToInt64(priority) is < int.MinValue or > int.MaxValue)
            {
                throw new InvalidOperationException($"The priority {typeof(TPriority).Name}.{priority} has a value that does not fit an int, which is how priorities are stored");
            }

            return new MessagePriorityOption { Name = label ?? priority.ToString().ToUpperInvariant(), Value = index, IsHidden = isHidden, Key = priority };
        })];
        if (built.GroupBy(option => option.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1) is { } duplicate)
        {
            throw new InvalidOperationException($"Two priorities are named {duplicate.Key}: every priority needs its own name");
        }

        return built;
    }
}
