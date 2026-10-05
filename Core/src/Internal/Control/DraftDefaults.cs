namespace BlueHeighliner.Comlink;

/// <summary>What a new draft starts with, as the host's draft handler states it.</summary>
/// <param name="Tag">The tag, made to fit the tag rules, or an empty string for none.</param>
/// <param name="Priority">The priority level as the host's enum member, or <see langword="null"/> for the lowest the user may choose.</param>
/// <param name="SecurityLevel">The security level as the host's enum member, or <see langword="null"/> for the highest the user may use.</param>
internal sealed record DraftDefaults(string Tag, Enum? Priority, Enum? SecurityLevel)
{
    /// <summary>Gets the defaults when the host states none.</summary>
    public static DraftDefaults None { get; } = new(string.Empty, null, null);
}
