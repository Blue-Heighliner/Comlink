namespace BlueHeighliner.Comlink;

/// <summary>Configures the aspects of one security level (see <see cref="ISecurityLevelsBuilder{TLevel}.Level"/>); it is also a <see cref="ISecurityLevelsBuilder{TLevel}"/>, so the next level can follow straight on.</summary>
/// <typeparam name="TLevel">The enum whose members are the levels.</typeparam>
public interface ISecurityLevelBuilder<TLevel> : ISecurityLevelsBuilder<TLevel> where TLevel : struct, Enum
{
    /// <summary>Sets the name of the level, which is how network files and frames refer to it. Defaults to the member name in uppercase.</summary>
    /// <param name="label">The name.</param>
    ISecurityLevelBuilder<TLevel> Label(string label);

    /// <summary>Sets the hex color the level is shown in, in the top banner. Defaults to a neutral gray.</summary>
    /// <param name="color">The color, such as <c>#2E7D32</c>.</param>
    ISecurityLevelBuilder<TLevel> Color(string color);
}
