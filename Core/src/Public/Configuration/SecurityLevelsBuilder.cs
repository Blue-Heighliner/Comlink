namespace BlueHeighliner.Comlink;

/// <summary>
/// Configures the security levels the members of <typeparamref name="TLevel"/> stand for. Every member is a level, lowest first, so each ranks higher than the one declared before it.
/// Name a member with <see cref="Level"/> to configure it, then continue with the next, for example <c>levels.Level(SecurityLevel.Public).Color("#2E7D32").Level(SecurityLevel.Restricted).Color("#C62828")</c>.
/// </summary>
/// <typeparam name="TLevel">The enum whose members are the levels.</typeparam>
public interface ISecurityLevelsBuilder<TLevel> where TLevel : struct, Enum
{
    /// <summary>Selects <paramref name="level"/> to configure its aspects.</summary>
    /// <param name="level">The level, a member of <typeparamref name="TLevel"/>.</param>
    ISecurityLevelBuilder<TLevel> Level(TLevel level);
}
