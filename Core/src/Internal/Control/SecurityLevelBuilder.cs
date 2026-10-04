namespace BlueHeighliner.Comlink;

/// <summary>Implements <see cref="ISecurityLevelsBuilder{TLevel}"/> and <see cref="ISecurityLevelBuilder{TLevel}"/>, recording what is stated about each level and turning it into the engine's security levels.</summary>
internal sealed class SecurityLevelBuilder<TLevel> : ISecurityLevelBuilder<TLevel> where TLevel : struct, Enum
{
    private readonly Dictionary<TLevel, (string? Label, string? Color)> options = [];
    private TLevel current;

    /// <inheritdoc />
    public ISecurityLevelBuilder<TLevel> Level(TLevel level)
    {
        current = level;
        return this;
    }

    /// <inheritdoc />
    public ISecurityLevelBuilder<TLevel> Label(string label)
    {
        options[current] = (label, options.GetValueOrDefault(current).Color);
        return this;
    }

    /// <inheritdoc />
    public ISecurityLevelBuilder<TLevel> Color(string color)
    {
        options[current] = (options.GetValueOrDefault(current).Label, color);
        return this;
    }

    /// <summary>Builds the security levels, one per member of the enum, lowest first.</summary>
    public List<SecurityLevel> Build()
        => [.. Enum.GetValues<TLevel>().Select(level =>
        {
            (string? label, string? color) = options.GetValueOrDefault(level);
            return new SecurityLevel { Name = label ?? level.ToString().ToUpperInvariant(), Color = color ?? "#5A5A5A" };
        })];
}
