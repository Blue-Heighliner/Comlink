namespace BlueHeighliner.Comlink;

/// <summary>Collects what is stated through <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}"/> and <see cref="ILevelBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}"/>, recording what is stated about each level and turning it into the engine's levels.</summary>
internal sealed class LevelBuilder<TLevel> where TLevel : struct, Enum
{
    private readonly Dictionary<TLevel, (string? Label, string? Background, string? Foreground, bool IsHidden)> options = [];
    private readonly List<TLevel> order = [];
    private TLevel current;

    /// <inheritdoc cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Level(TLevel)"/>
    public LevelBuilder<TLevel> Level(TLevel level)
    {
        current = level;
        if (!order.Contains(level))
        {
            order.Add(level);
        }
        return this;
    }

    /// <inheritdoc cref="ILevelBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Label"/>
    public LevelBuilder<TLevel> Label(string label)
    {
        (_, string? background, string? foreground, bool isHidden) = options.GetValueOrDefault(current);
        options[current] = (label, background, foreground, isHidden);
        return this;
    }

    /// <inheritdoc cref="ILevelBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Background"/>
    public LevelBuilder<TLevel> Background(string background)
    {
        (string? label, _, string? foreground, bool isHidden) = options.GetValueOrDefault(current);
        options[current] = (label, background, foreground, isHidden);
        return this;
    }

    /// <inheritdoc cref="ILevelBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Foreground"/>
    public LevelBuilder<TLevel> Foreground(string foreground)
    {
        (string? label, string? background, _, bool isHidden) = options.GetValueOrDefault(current);
        options[current] = (label, background, foreground, isHidden);
        return this;
    }

    /// <inheritdoc cref="ILevelBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.IsHidden"/>
    public LevelBuilder<TLevel> IsHidden(bool isHidden)
    {
        (string? label, string? background, string? foreground, _) = options.GetValueOrDefault(current);
        options[current] = (label, background, foreground, isHidden);
        return this;
    }

    /// <summary>Builds the levels, one per stated member, in the order stated, lowest first.</summary>
    /// <exception cref="InvalidOperationException">A level's value does not fit an <see cref="int"/>, which is how it is stored, or two levels have the same name.</exception>
    public List<LevelDefinition> Build()
    {
        List<LevelDefinition> built =
        [.. order.Select(level =>
        {
            (string? label, string? background, string? foreground, bool isHidden) = options.GetValueOrDefault(level);
            if (Convert.ToInt64(level) is < int.MinValue or > int.MaxValue)
            {
                throw new InvalidOperationException($"The level {typeof(TLevel).Name}.{level} has a value that does not fit an int, which is how levels are stored");
            }

            return new LevelDefinition { Key = level, Label = label ?? level.ToString().ToUpperInvariant(), Background = background ?? "#5A5A5A", Foreground = foreground ?? "#FFFFFF", IsHidden = isHidden };
        })];
        return built;
    }
}
