namespace BlueHeighliner.Comlink;

/// <summary>Collects what is stated through <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}"/> and <see cref="IAspectBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}"/>, recording the label of each aspect and turning it into the engine's aspects.</summary>
internal sealed class AspectBuilder<TAspect> where TAspect : struct, Enum
{
    private readonly Dictionary<TAspect, string> labels = [];
    private readonly List<TAspect> order = [];
    private TAspect current;

    /// <inheritdoc cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Aspect"/>
    public AspectBuilder<TAspect> Aspect(TAspect aspect)
    {
        current = aspect;
        if (!order.Contains(aspect))
        {
            order.Add(aspect);
        }
        return this;
    }

    /// <inheritdoc cref="IAspectBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Label"/>
    public AspectBuilder<TAspect> Label(string label)
    {
        labels[current] = label;
        return this;
    }

    /// <summary>Builds the aspects, one per stated member, in the order stated.</summary>
    /// <exception cref="InvalidOperationException">An aspect's value does not fit an <see cref="int"/>, which is how it is stored, or two aspects have the same name.</exception>
    public List<AspectDefinition> Build()
    {
        List<AspectDefinition> built =
        [.. order.Select(aspect =>
        {
            if (Convert.ToInt64(aspect) is < int.MinValue or > int.MaxValue)
            {
                throw new InvalidOperationException($"The aspect {typeof(TAspect).Name}.{aspect} has a value that does not fit an int, which is how aspects are stored");
            }

            return new AspectDefinition { Key = aspect, Label = labels.GetValueOrDefault(aspect) ?? aspect.ToString().ToUpperInvariant() };
        })];
        return built;
    }
}
