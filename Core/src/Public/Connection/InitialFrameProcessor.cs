namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// Carries out an initial frame exchange on every new connection, so nodes can introduce themselves with frames: the host decides what to send, what
/// to make of what arrives, and when the connection counts as connected and as which user. Everything sent is a serialized instance of the frame type,
/// and nothing else crosses the connection, so what the other node sends is recognized by position: once a connection has formed, each frame a node
/// receives before it is marked connected is given to <see cref="OnInitial"/> on the accepting node or <see cref="OnReply"/> on the opening node. Every node on a
/// network must be configured alike. State one with <see cref="IFrameBuilder{TFrame}.InitialProcessor"/>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface IInitialFrameProcessor<TFrame> where TFrame : class
{
    /// <summary>Called on both nodes when the connection has formed, before anything has been received. The opening node (<see cref="IInitialFrameContext{TFrame}.IsOpener"/>) usually sends its initial message here.</summary>
    /// <param name="context">Controls the connection.</param>
    void OnConnected(IInitialFrameContext<TFrame> context);

    /// <summary>Called on the accepting node for each frame the opening node sent, while the connection is not yet marked connected.</summary>
    /// <param name="context">Controls the connection.</param>
    /// <param name="frame">What arrived.</param>
    void OnInitial(IInitialFrameContext<TFrame> context, TFrame frame);

    /// <summary>Called on the opening node for each frame the accepting node sent, while the connection is not yet marked connected.</summary>
    /// <param name="context">Controls the connection.</param>
    /// <param name="frame">What arrived.</param>
    void OnReply(IInitialFrameContext<TFrame> context, TFrame frame);
}
