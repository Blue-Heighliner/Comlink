namespace BlueHeighliner.Comlink;

/// <summary>Collects what is stated through <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}"/> and <see cref="IMessageLevelBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}"/>, recording what is stated about each level and turning it into the engine's message levels.</summary>
internal sealed class MessageLevelBuilder<TLevel> where TLevel : struct, Enum
{
    private readonly Dictionary<TLevel, (string? Label, string? Background, string? Foreground)> options = [];
    private readonly List<TLevel> order = [];
    private TLevel current;

    /// <inheritdoc cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Level(TLevel)"/>
    public MessageLevelBuilder<TLevel> Level(TLevel level)
    {
        current = level;
        if (!order.Contains(level))
        {
            order.Add(level);
        }
        return this;
    }

    /// <inheritdoc cref="IMessageLevelBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Label"/>
    public MessageLevelBuilder<TLevel> Label(string label)
    {
        (_, string? background, string? foreground) = options.GetValueOrDefault(current);
        options[current] = (label, background, foreground);
        return this;
    }

    /// <inheritdoc cref="IMessageLevelBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Background"/>
    public MessageLevelBuilder<TLevel> Background(string background)
    {
        (string? label, _, string? foreground) = options.GetValueOrDefault(current);
        options[current] = (label, background, foreground);
        return this;
    }

    /// <inheritdoc cref="IMessageLevelBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Foreground"/>
    public MessageLevelBuilder<TLevel> Foreground(string foreground)
    {
        (string? label, string? background, _) = options.GetValueOrDefault(current);
        options[current] = (label, background, foreground);
        return this;
    }

    /// <summary>Builds the message levels, one per stated member, in the order stated, lowest first.</summary>
    /// <exception cref="InvalidOperationException">A level's value does not fit an <see cref="int"/>, which is how it is stored, or two levels have the same name.</exception>
    public List<MessageLevel> Build()
    {
        List<MessageLevel> built =
        [.. order.Select(level =>
        {
            (string? label, string? background, string? foreground) = options.GetValueOrDefault(level);
            if (Convert.ToInt64(level) is < int.MinValue or > int.MaxValue)
            {
                throw new InvalidOperationException($"The message level {typeof(TLevel).Name}.{level} has a value that does not fit an int, which is how message levels are stored");
            }

            return new MessageLevel { Key = level, Name = label ?? level.ToString().ToUpperInvariant(), Background = background ?? "#5A5A5A", Foreground = foreground ?? "#FFFFFF" };
        })];
        if (built.GroupBy(level => level.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1) is { } duplicate)
        {
            throw new InvalidOperationException($"Two message levels are named {duplicate.Key}: every message level needs its own name");
        }

        return built;
    }
}
