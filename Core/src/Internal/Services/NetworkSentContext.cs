namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="INetworkSentContext{TFrame, TPriority, TLevel, TAspect}" />
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
internal sealed class NetworkSentContext<TFrame, TPriority, TLevel, TAspect> : NetworkEngineContext<TFrame, TPriority, TLevel, TAspect>, INetworkSentContext<TFrame, TPriority, TLevel, TAspect> where TFrame : class where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Creates the context for a message the user sent.</summary>
    /// <param name="environment">What the handler acts on.</param>
    /// <param name="message">The message the user sent.</param>
    public NetworkSentContext(INetworkEnvironment environment, Message message)
        : base(environment)
    {
        Message = message.ToTyped<TPriority, TLevel, TAspect>();
        Destinations = GetDestinations(Message, out IReadOnlySet<string> excluded);
        Excluded = excluded;
    }

    /// <inheritdoc />
    public Message<TPriority, TLevel, TAspect> Message { get; }

    /// <inheritdoc />
    public IReadOnlySet<string> Destinations { get; }

    /// <summary>Gets the users the message cannot be sent to, which are marked failed before the handler runs.</summary>
    public IReadOnlySet<string> Excluded { get; }
}
