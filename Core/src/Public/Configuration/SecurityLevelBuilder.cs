namespace BlueHeighliner.Comlink;

/// <summary>Configures the aspects of one security level (see <see cref="ISecurityLevelsBuilder{TFrame, TPacket, TPriority, TLevel}.Level"/>); it is also a <see cref="ISecurityLevelsBuilder{TFrame, TPacket, TPriority, TLevel}"/>, so the next level can follow straight on.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/>.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the security levels.</typeparam>
public interface ISecurityLevelBuilder<TFrame, TPacket, TPriority, TLevel> : ISecurityLevelsBuilder<TFrame, TPacket, TPriority, TLevel> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum
{
    /// <summary>Sets the name of the level, which is how network files and frames refer to it. Defaults to the member name in uppercase.</summary>
    /// <param name="label">The name.</param>
    ISecurityLevelBuilder<TFrame, TPacket, TPriority, TLevel> Label(string label);

    /// <summary>Sets the hex color the level is shown in, in the top banner. Defaults to a neutral gray.</summary>
    /// <param name="color">The color, such as <c>#2E7D32</c>.</param>
    ISecurityLevelBuilder<TFrame, TPacket, TPriority, TLevel> Color(string color);
}
