namespace BlueHeighliner.Comlink;

/// <summary>Configures one message aspect (see <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}.Aspect"/>); it is also a <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}"/>, so the next aspect can follow straight on.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/>.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects.</typeparam>
/// <typeparam name="TProfile">The record type the host stated for user profiles in its <c>Types</c> call, or <see cref="NoProfile"/>.</typeparam>
public interface IMessageAspectBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum where TProfile : class
{
    /// <summary>Sets the name of the aspect, which is how the draft view, the message view and frames refer to it. Defaults to the member name in uppercase.</summary>
    /// <param name="label">The name.</param>
    IMessageAspectBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Label(string label);
}
