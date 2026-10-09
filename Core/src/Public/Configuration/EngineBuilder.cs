namespace BlueHeighliner.Comlink;

/// <summary>
/// The start of the fluent surface a host uses to say how the engine runs, handed to <see cref="IEngineConfiguration.Configure"/>. The host must first fix the types
/// the whole configuration is typed by with <see cref="Types{TFrame, TPriority, TLevel, TAspect}"/>, which returns the builder everything else is stated on, so that handlers, priorities and message levels
/// are all checked against them.
/// </summary>
public interface IEngineBuilder
{
    /// <summary>
    /// Fixes the types of a configuration that does not packetize: the host's frame type, the enum whose members are the priority levels and the enum whose members are the message levels.
    /// A configuration that does not use priorities or message levels states <see cref="NoPriority"/> or <see cref="NoMessageLevel"/>.
    /// </summary>
    /// <typeparam name="TFrame">The host's frame type.</typeparam>
    /// <typeparam name="TPriority">The enum whose members are the priority levels, stated with <c>Priority</c>, lowest first. Its integer values are stored, so a member's value never changes.</typeparam>
    /// <typeparam name="TLevel">The enum whose members are the message levels, stated with <c>Level</c>, lowest first. Its integer values are stored, so a member's value never changes.</typeparam>
    /// <typeparam name="TAspect">The enum whose members are the message aspects, or <see cref="NoMessageAspect"/> for none.</typeparam>
    IEngineBuilder<TFrame, NoPacket, TPriority, TLevel, TAspect> Types<TFrame, TPriority, TLevel, TAspect>() where TFrame : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum;

    /// <summary>Fixes the types of a configuration that packetizes: like <see cref="Types{TFrame, TPriority, TLevel, TAspect}"/>, plus the host's packet type.</summary>
    /// <typeparam name="TFrame">The host's frame type.</typeparam>
    /// <typeparam name="TPacket">The host's packet type.</typeparam>
    /// <typeparam name="TPriority">The enum whose members are the priority levels, stated with <c>Priority</c>, lowest first. Its integer values are stored, so a member's value never changes.</typeparam>
    /// <typeparam name="TLevel">The enum whose members are the message levels, stated with <c>Level</c>, lowest first. Its integer values are stored, so a member's value never changes.</typeparam>
    /// <typeparam name="TAspect">The enum whose members are the message aspects, or <see cref="NoMessageAspect"/> for none.</typeparam>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Types<TFrame, TPacket, TPriority, TLevel, TAspect>() where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum;
}
