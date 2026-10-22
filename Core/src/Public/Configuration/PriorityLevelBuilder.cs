namespace BlueHeighliner.Comlink;

/// <summary>Configures the aspects of one priority level (see <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Priority"/>); it is also a <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}"/>, so the next level can follow straight on.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/>.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects, or <see cref="NoMessageAspect"/> for none.</typeparam>
/// <typeparam name="TProfile">The record type the host stated for user profiles in its <c>Types</c> call, or <see cref="NoProfile"/>.</typeparam>
public interface IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum where TProfile : class
{
    /// <summary>Sets the name of the level, which is shown to users. Defaults to the member name in uppercase.</summary>
    /// <param name="label">The name.</param>
    IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Label(string label);

    /// <summary>Sets whether the GUI offers the level to a user composing a message. It never restricts code. Defaults to <see cref="PriorityMode.User"/>.</summary>
    /// <param name="mode">Whether the GUI offers the level.</param>
    IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Mode(PriorityMode mode);
}
