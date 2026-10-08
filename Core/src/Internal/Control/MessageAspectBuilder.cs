namespace BlueHeighliner.Comlink;

/// <summary>Collects what is stated through <see cref="IMessageAspectsBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}"/> and <see cref="IMessageAspectBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}"/>, recording the label of each aspect and turning it into the engine's message aspects.</summary>
internal sealed class MessageAspectBuilder<TAspect> where TAspect : struct, Enum
{
    private readonly Dictionary<TAspect, string> labels = [];
    private readonly List<TAspect> order = [];
    private TAspect current;

    /// <inheritdoc cref="IMessageAspectsBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Aspect"/>
    public MessageAspectBuilder<TAspect> Aspect(TAspect aspect)
    {
        current = aspect;
        if (!order.Contains(aspect))
        {
            order.Add(aspect);
        }
        return this;
    }

    /// <inheritdoc cref="IMessageAspectBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Label"/>
    public MessageAspectBuilder<TAspect> Label(string label)
    {
        labels[current] = label;
        return this;
    }

    /// <summary>Builds the message aspects, one per stated member, in the order stated.</summary>
    /// <exception cref="InvalidOperationException">An aspect's value does not fit an <see cref="int"/>, which is how it is stored, or two aspects have the same name.</exception>
    public List<MessageAspect> Build()
    {
        List<MessageAspect> built =
        [.. order.Select(aspect =>
        {
            if (Convert.ToInt64(aspect) is < int.MinValue or > int.MaxValue)
            {
                throw new InvalidOperationException($"The message aspect {typeof(TAspect).Name}.{aspect} has a value that does not fit an int, which is how message aspects are stored");
            }

            return new MessageAspect { Key = aspect, Name = labels.GetValueOrDefault(aspect) ?? aspect.ToString().ToUpperInvariant() };
        })];
        if (built.GroupBy(aspect => aspect.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1) is { } duplicate)
        {
            throw new InvalidOperationException($"Two message aspects are named {duplicate.Key}: every message aspect needs its own name");
        }

        return built;
    }
}
