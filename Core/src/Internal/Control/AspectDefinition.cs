namespace BlueHeighliner.Comlink;

/// <summary>An aspect a message can carry (see <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Aspect"/>). It is the member of the host's aspect enum it was stated with: that member is its identity, and its <see cref="Label"/> is only what the user interface shows.</summary>
internal sealed record AspectDefinition
{
    /// <summary>Gets the text the user interface shows for this aspect. It identifies nothing.</summary>
    public required string Label { get; init; }

    /// <summary>Gets whether the GUI hides this aspect from a user composing a message; code may use any aspect.</summary>
    public bool IsHidden { get; init; }

    /// <summary>Gets the member of the host's aspect enum this aspect is.</summary>
    public required Enum Key { get; init; }

    /// <summary>Gets the integer value of <see cref="Key"/>, which is how the aspect is stored in messages and drafts and so must never change for a member of the host's enum.</summary>
    public int Value => Convert.ToInt32(Key);
}

/// <summary>Extension helpers for looking up an aspect in the set of <see cref="AspectDefinition"/> values, by the aspect's enum member.</summary>
internal static class AspectDefinitionExtensions
{
    /// <summary>Returns the aspect in <paramref name="aspects"/> that is <paramref name="aspect"/>, or <see langword="null"/> for <see langword="null"/> or an aspect that is none of them.</summary>
    public static AspectDefinition? Find(this IReadOnlyList<AspectDefinition> aspects, Enum? aspect) => aspect is null ? null : aspects.FirstOrDefault(candidate => candidate.Key.Equals(aspect));

    /// <summary>Returns the aspect in <paramref name="aspects"/> whose stored integer value is <paramref name="value"/>, or <see langword="null"/> for <see langword="null"/> or a value no longer configured.</summary>
    public static AspectDefinition? Find(this IReadOnlyList<AspectDefinition> aspects, int? value) => value is null ? null : aspects.FirstOrDefault(candidate => candidate.Value == value);
}
