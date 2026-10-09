namespace BlueHeighliner.Comlink;

/// <summary>Collects what is stated through <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}"/> and <see cref="IMessageLevelBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}"/>, recording what is stated about each level and turning it into the engine's message levels.</summary>
internal sealed class MessageLevelBuilder<TLevel> where TLevel : struct, Enum
{
    private readonly Dictionary<TLevel, (string? Label, string? Color)> options = [];
    private readonly List<TLevel> order = [];
    private TLevel current;

    /// <inheritdoc cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Level(TLevel)"/>
    public MessageLevelBuilder<TLevel> Level(TLevel level)
    {
        current = level;
        if (!order.Contains(level))
        {
            order.Add(level);
        }
        return this;
    }

    /// <inheritdoc cref="IMessageLevelBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Label"/>
    public MessageLevelBuilder<TLevel> Label(string label)
    {
        options[current] = (label, options.GetValueOrDefault(current).Color);
        return this;
    }

    /// <inheritdoc cref="IMessageLevelBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Color"/>
    public MessageLevelBuilder<TLevel> Color(string color)
    {
        options[current] = (options.GetValueOrDefault(current).Label, color);
        return this;
    }

    /// <summary>Builds the message levels, one per stated member, in the order stated, lowest first.</summary>
    /// <exception cref="InvalidOperationException">A level's value does not fit an <see cref="int"/>, which is how it is stored, or two levels have the same name.</exception>
    public List<MessageLevel> Build()
    {
        List<MessageLevel> built =
        [.. order.Select(level =>
        {
            (string? label, string? color) = options.GetValueOrDefault(level);
            if (Convert.ToInt64(level) is < int.MinValue or > int.MaxValue)
            {
                throw new InvalidOperationException($"The message level {typeof(TLevel).Name}.{level} has a value that does not fit an int, which is how message levels are stored");
            }

            return new MessageLevel { Key = level, Name = label ?? level.ToString().ToUpperInvariant(), Color = color ?? "#5A5A5A" };
        })];
        if (built.GroupBy(level => level.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1) is { } duplicate)
        {
            throw new InvalidOperationException($"Two message levels are named {duplicate.Key}: every message level needs its own name");
        }

        return built;
    }
}
