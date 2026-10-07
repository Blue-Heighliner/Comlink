namespace BlueHeighliner.Comlink;

/// <summary>A message aspect a message can carry (see <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.MessageAspects"/>).</summary>
internal sealed record MessageAspect
{
    /// <summary>Gets the display name of this aspect, and the value stored in a message's message aspect field.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the member of the host's message aspect enum this aspect is.</summary>
    public required Enum Key { get; init; }

    /// <summary>Gets the integer value of <see cref="Key"/>, which is how the aspect is stored in drafts and so must never change for a member of the host's enum.</summary>
    public int Value => Convert.ToInt32(Key);
}
