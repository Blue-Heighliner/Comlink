namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="INetworkReadContext{TPriority, TLevel, TAspect}" />
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
/// <param name="Message">The message the user opened.</param>
internal sealed record NetworkReadContext<TPriority, TLevel, TAspect>(Message<TPriority, TLevel, TAspect> Message) : INetworkReadContext<TPriority, TLevel, TAspect> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum;
