namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="INetworkSentContext{TPriority, TLevel, TAspect}" />
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
/// <param name="Message">The message the user sent.</param>
/// <param name="Destinations">The users it is for and can be sent to.</param>
internal sealed record NetworkSentContext<TPriority, TLevel, TAspect>(Message<TPriority, TLevel, TAspect> Message, IReadOnlySet<string> Destinations) : INetworkSentContext<TPriority, TLevel, TAspect> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum;
