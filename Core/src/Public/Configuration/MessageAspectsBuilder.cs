namespace BlueHeighliner.Comlink;

/// <summary>
/// Configures the message aspects the members of <typeparamref name="TAspect"/> stand for, continuing the fluent chain of the engine builder. A message carries one aspect or none, as additional security information beside its message level, for example <c>Encrypted</c> or <c>Signed</c>.
/// Every aspect must be stated with <see cref="Aspect"/>, in the order they are offered: a member not stated is not an aspect. Their integer values are how aspects are stored in drafts, so a member of the enum must never have its value changed or reused, even after it is no longer used.
/// Name a member with <see cref="Aspect"/> to configure it, then continue with the next, for example <c>.MessageAspects().Aspect(MessageAspect.Encrypted).Aspect(MessageAspect.Signed).Label("SIGNED BY SENDER")</c>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/>.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects.</typeparam>
public interface IMessageAspectsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Selects <paramref name="aspect"/> to configure it.</summary>
    /// <param name="aspect">The aspect, a member of <typeparamref name="TAspect"/>.</param>
    IMessageAspectBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Aspect(TAspect aspect);
}
