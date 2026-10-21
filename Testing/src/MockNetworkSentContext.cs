namespace BlueHeighliner.Comlink.Testing;

/// <summary>A mock of the <see cref="INetworkSentContext{TPriority, TLevel, TAspect}"/> an <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}"/> is handed when the user sends a message.</summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
public sealed class MockNetworkSentContext<TPriority, TLevel, TAspect> : Mock<INetworkSentContext<TPriority, TLevel, TAspect>> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Initializes the mock.</summary>
    /// <param name="message">The message the user sent.</param>
    /// <param name="destinations">The users it is for and can be sent to.</param>
    public MockNetworkSentContext(Message<TPriority, TLevel, TAspect> message, IReadOnlySet<string> destinations)
    {
        Message = message;
        Destinations = destinations;
        Setup(context => context.Message).Returns(() => Message);
        Setup(context => context.Destinations).Returns(() => Destinations);
    }

    /// <summary>Gets or sets what <see cref="INetworkSentContext{TPriority, TLevel, TAspect}.Message"/> returns.</summary>
    public Message<TPriority, TLevel, TAspect> Message { get; set; }

    /// <summary>Gets or sets what <see cref="INetworkSentContext{TPriority, TLevel, TAspect}.Destinations"/> returns.</summary>
    public IReadOnlySet<string> Destinations { get; set; }
}
