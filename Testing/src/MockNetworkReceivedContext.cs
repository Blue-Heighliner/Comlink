namespace BlueHeighliner.Comlink.Testing;

/// <summary>A mock of the <see cref="INetworkReceivedContext{TFrame}"/> an <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}"/> is handed when a frame is received.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public sealed class MockNetworkReceivedContext<TFrame> : Mock<INetworkReceivedContext<TFrame>> where TFrame : class
{
    /// <summary>Initializes the mock.</summary>
    /// <param name="frame">The frame that was received.</param>
    /// <param name="sourceUser">The user the frame arrived from.</param>
    /// <param name="origin">Where the frame came from.</param>
    public MockNetworkReceivedContext(TFrame frame, UserInfo sourceUser, FrameOrigin origin = FrameOrigin.Peer)
    {
        Frame = frame;
        SourceUser = sourceUser;
        Origin = origin;
        Setup(context => context.Frame).Returns(() => Frame);
        Setup(context => context.SourceUser).Returns(() => SourceUser);
        Setup(context => context.Origin).Returns(() => Origin);
    }

    /// <summary>Gets or sets what <see cref="INetworkReceivedContext{TFrame}.Frame"/> returns.</summary>
    public TFrame Frame { get; set; }

    /// <summary>Gets or sets what <see cref="INetworkReceivedContext{TFrame}.SourceUser"/> returns.</summary>
    public UserInfo SourceUser { get; set; }

    /// <summary>Gets or sets what <see cref="INetworkReceivedContext{TFrame}.Origin"/> returns.</summary>
    public FrameOrigin Origin { get; set; }
}
