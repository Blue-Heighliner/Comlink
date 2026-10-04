namespace BlueHeighliner.Comlink;

/// <summary>Collects what is stated through <see cref="ISecurityLevelsBuilder{TFrame, TPacket, TPriority, TLevel}"/> and <see cref="ISecurityLevelBuilder{TFrame, TPacket, TPriority, TLevel}"/>, recording what is stated about each level and turning it into the engine's security levels.</summary>
internal sealed class SecurityLevelBuilder<TLevel> where TLevel : struct, Enum
{
    private readonly Dictionary<TLevel, (string? Label, string? Color)> options = [];
    private readonly List<TLevel> order = [];
    private TLevel current;

    /// <inheritdoc cref="ISecurityLevelsBuilder{TFrame, TPacket, TPriority, TLevel}.Level(TLevel)"/>
    public SecurityLevelBuilder<TLevel> Level(TLevel level)
    {
        current = level;
        if (!order.Contains(level)) { order.Add(level); }
        return this;
    }

    /// <inheritdoc cref="ISecurityLevelBuilder{TFrame, TPacket, TPriority, TLevel}.Label"/>
    public SecurityLevelBuilder<TLevel> Label(string label)
    {
        options[current] = (label, options.GetValueOrDefault(current).Color);
        return this;
    }

    /// <inheritdoc cref="ISecurityLevelBuilder{TFrame, TPacket, TPriority, TLevel}.Color"/>
    public SecurityLevelBuilder<TLevel> Color(string color)
    {
        options[current] = (options.GetValueOrDefault(current).Label, color);
        return this;
    }

    /// <summary>Builds the security levels, one per stated member, in the order stated, lowest first.</summary>
    /// <exception cref="InvalidOperationException">A level's value does not fit an <see cref="int"/>, which is how it is stored, or two levels have the same name.</exception>
    public List<SecurityLevel> Build()
    {
        List<SecurityLevel> built =
        [.. order.Select(level =>
        {
            (string? label, string? color) = options.GetValueOrDefault(level);
            if (Convert.ToInt64(level) is < int.MinValue or > int.MaxValue) { throw new InvalidOperationException($"The security level {typeof(TLevel).Name}.{level} has a value that does not fit an int, which is how security levels are stored"); }

            return new SecurityLevel { Key = level, Name = label ?? level.ToString().ToUpperInvariant(), Color = color ?? "#5A5A5A" };
        })];
        if (built.GroupBy(level => level.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1) is { } duplicate) { throw new InvalidOperationException($"Two security levels are named {duplicate.Key}: every security level needs its own name"); }

        return built;
    }
}
