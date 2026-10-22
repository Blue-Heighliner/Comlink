namespace BlueHeighliner.Comlink;

/// <summary>What a new draft starts with, as the host's draft handler states it.</summary>
/// <param name="Tag">The tag, made to fit the tag rules, or an empty string for none.</param>
/// <param name="Priority">The priority level as the host's enum member, or <see langword="null"/> for the lowest the user may choose.</param>
/// <param name="Level">The level as the host's enum member, or <see langword="null"/> for the highest the user may use.</param>
/// <param name="Aspect">The aspect as the host's enum member, or <see langword="null"/> for none.</param>
internal sealed record DraftDefaults(string Tag, Enum? Priority, Enum? Level, Enum? Aspect)
{
    /// <summary>Gets the defaults when the host states none.</summary>
    public static DraftDefaults None { get; } = new(string.Empty, null, null, null);
}
