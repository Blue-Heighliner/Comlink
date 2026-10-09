namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="INetworkReadContext{TFrame, TPriority, TLevel, TAspect}" />
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
/// <param name="environment">What the handler acts on.</param>
/// <param name="message">The message the user opened.</param>
internal sealed class NetworkReadContext<TFrame, TPriority, TLevel, TAspect>(INetworkEnvironment environment, Message message) : NetworkEngineContext<TFrame, TPriority, TLevel, TAspect>(environment), INetworkReadContext<TFrame, TPriority, TLevel, TAspect> where TFrame : class where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <inheritdoc />
    public Message<TPriority, TLevel, TAspect> Message { get; } = message.ToTyped<TPriority, TLevel, TAspect>();
}
