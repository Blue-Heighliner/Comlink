namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="INetworkSentContext{TPriority, TLevel, TAspect}"/> a frame handler is handed for a message the user sent.</summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
/// <param name="message">The message that was sent.</param>
/// <param name="destinations">The users it is to be delivered to.</param>
public sealed class FakeNetworkSentContext<TPriority, TLevel, TAspect>(Message<TPriority, TLevel, TAspect> message, IReadOnlySet<string> destinations) : INetworkSentContext<TPriority, TLevel, TAspect>
    where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <inheritdoc />
    public Message<TPriority, TLevel, TAspect> Message { get; set; } = message;

    /// <inheritdoc />
    public IReadOnlySet<string> Destinations { get; set; } = destinations;
}
