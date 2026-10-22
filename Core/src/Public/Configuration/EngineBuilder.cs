namespace BlueHeighliner.Comlink;

/// <summary>
/// The start of the fluent surface a host uses to say how the engine runs, handed to <see cref="IEngineConfiguration.Configure"/>. The host must first fix the types
/// the whole configuration is typed by with <see cref="Types{TFrame, TPriority, TLevel, TAspect, TProfile}"/>, which returns the builder everything else is stated on, so that handlers, priorities and levels
/// are all checked against them.
/// </summary>
public interface IEngineBuilder
{
    /// <summary>
    /// Fixes the types of a configuration that does not packetize: the host's frame type, the enum whose members are the priority levels and the enum whose members are the levels.
    /// A configuration that does not use priorities, levels or user profiles states <see cref="NoPriority"/>, <see cref="NoLevel"/> or <see cref="NoProfile"/>.
    /// </summary>
    /// <typeparam name="TFrame">The host's frame type.</typeparam>
    /// <typeparam name="TPriority">The enum whose members are the priority levels, stated with <c>Priority</c>, lowest first. Its integer values are stored, so a member's value never changes.</typeparam>
    /// <typeparam name="TLevel">The enum whose members are the levels, stated with <c>Level</c>, lowest first. Its integer values are stored, so a member's value never changes.</typeparam>
    /// <typeparam name="TAspect">The enum whose members are the aspects, or <see cref="NoAspect"/> for none.</typeparam>
    /// <typeparam name="TProfile">The record type the <c>Profile</c> of each user in the network configuration file is read into (names matched without regard to case, enums written by name; lists, nested records and nullable members read as usual), available as <see cref="UserInfo{TLevel, TProfile}.Profile"/>; or <see cref="NoProfile"/> for users with no profile. A profile that cannot be read into it makes the network configuration invalid, at start-up and on a refresh.</typeparam>
    IEngineBuilder<TFrame, NoPacket, TPriority, TLevel, TAspect, TProfile> Types<TFrame, TPriority, TLevel, TAspect, TProfile>() where TFrame : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum where TProfile : class;

    /// <summary>Fixes the types of a configuration that packetizes: like <see cref="Types{TFrame, TPriority, TLevel, TAspect, TProfile}"/>, plus the host's packet type.</summary>
    /// <typeparam name="TFrame">The host's frame type.</typeparam>
    /// <typeparam name="TPacket">The host's packet type.</typeparam>
    /// <typeparam name="TPriority">The enum whose members are the priority levels, stated with <c>Priority</c>, lowest first. Its integer values are stored, so a member's value never changes.</typeparam>
    /// <typeparam name="TLevel">The enum whose members are the levels, stated with <c>Level</c>, lowest first. Its integer values are stored, so a member's value never changes.</typeparam>
    /// <typeparam name="TAspect">The enum whose members are the aspects, or <see cref="NoAspect"/> for none.</typeparam>
    /// <typeparam name="TProfile">The record type the <c>Profile</c> of each user in the network configuration file is read into (names matched without regard to case, enums written by name; lists, nested records and nullable members read as usual), available as <see cref="UserInfo{TLevel, TProfile}.Profile"/>; or <see cref="NoProfile"/> for users with no profile. A profile that cannot be read into it makes the network configuration invalid, at start-up and on a refresh.</typeparam>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Types<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile>() where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum where TProfile : class;
}
