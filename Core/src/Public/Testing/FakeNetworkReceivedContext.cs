namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="INetworkReceivedContext{TFrame}"/> a frame handler is handed for a frame that arrived.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <param name="frame">The frame that arrived.</param>
/// <param name="sourceUser">The user it came from.</param>
/// <param name="origin">Where it came in from.</param>
public sealed class FakeNetworkReceivedContext<TFrame>(TFrame frame, UserInfo sourceUser, FrameOrigin origin = FrameOrigin.Peer) : INetworkReceivedContext<TFrame> where TFrame : class
{
    /// <inheritdoc />
    public TFrame Frame { get; set; } = frame;

    /// <inheritdoc />
    public UserInfo SourceUser { get; set; } = sourceUser;

    /// <inheritdoc />
    public FrameOrigin Origin { get; set; } = origin;
}
