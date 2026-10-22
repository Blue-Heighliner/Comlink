namespace BlueHeighliner.Comlink;

/// <summary>Configures the aspects of one level (see <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Level"/>); it is also a <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}"/>, so the next level can follow straight on.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/>.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the aspects, or <see cref="NoAspect"/> for none.</typeparam>
/// <typeparam name="TProfile">The record type the host stated for user profiles in its <c>Types</c> call, or <see cref="NoProfile"/>.</typeparam>
public interface ILevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum where TProfile : class
{
    /// <summary>Sets the name of the level, which is how network files and frames refer to it. Defaults to the member name in uppercase.</summary>
    /// <param name="label">The name.</param>
    ILevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Label(string label);

    /// <summary>Sets the hex background color the level is shown on: the top banner and the level's label in a message. Defaults to a neutral gray.</summary>
    /// <param name="background">The color, such as <c>#2E7D32</c>.</param>
    ILevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Background(string background);

    /// <summary>Sets the hex color of the text shown on the level's background. Defaults to white.</summary>
    /// <param name="foreground">The color, such as <c>#FFFFFF</c>.</param>
    ILevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Foreground(string foreground);
}
