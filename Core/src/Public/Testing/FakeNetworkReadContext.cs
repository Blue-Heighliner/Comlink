namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="INetworkReadContext{TPriority, TLevel, TAspect}"/> a frame handler is told a message was read with.</summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its aspects.</typeparam>
/// <param name="message">The message that was read.</param>
public sealed class FakeNetworkReadContext<TPriority, TLevel, TAspect>(Message<TPriority, TLevel, TAspect> message) : INetworkReadContext<TPriority, TLevel, TAspect>
    where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <inheritdoc />
    public Message<TPriority, TLevel, TAspect> Message { get; set; } = message;
}
