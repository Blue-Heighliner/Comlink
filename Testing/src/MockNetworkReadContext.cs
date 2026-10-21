namespace BlueHeighliner.Comlink.Testing;

/// <summary>A mock of the <see cref="INetworkReadContext{TPriority, TLevel, TAspect}"/> an <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}"/> is handed when the user opens a message.</summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
public sealed class MockNetworkReadContext<TPriority, TLevel, TAspect> : Mock<INetworkReadContext<TPriority, TLevel, TAspect>> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Initializes the mock.</summary>
    /// <param name="message">The message the user opened.</param>
    public MockNetworkReadContext(Message<TPriority, TLevel, TAspect> message)
    {
        Message = message;
        Setup(context => context.Message).Returns(() => Message);
    }

    /// <summary>Gets or sets what <see cref="INetworkReadContext{TPriority, TLevel, TAspect}.Message"/> returns.</summary>
    public Message<TPriority, TLevel, TAspect> Message { get; set; }
}
