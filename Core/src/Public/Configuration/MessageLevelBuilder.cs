namespace BlueHeighliner.Comlink;

/// <summary>Configures the aspects of one message level (see <see cref="IMessageLevelsBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Level"/>); it is also a <see cref="IMessageLevelsBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}"/>, so the next level can follow straight on.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/>.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects, or <see cref="NoMessageAspect"/> for none.</typeparam>
public interface IMessageLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> : IMessageLevelsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Sets the name of the level, which is how network files and frames refer to it. Defaults to the member name in uppercase.</summary>
    /// <param name="label">The name.</param>
    IMessageLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Label(string label);

    /// <summary>Sets the hex color the level is shown in, in the top banner. Defaults to a neutral gray.</summary>
    /// <param name="color">The color, such as <c>#2E7D32</c>.</param>
    IMessageLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Color(string color);
}
