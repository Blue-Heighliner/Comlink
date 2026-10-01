namespace BlueHeighliner.Comlink.Control;

/// <summary>What every <see cref="INetworkProcessor{TFrame}"/> method is handed: the engine, and a way to originate a new frame.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface INetworkContext<TFrame> : IEngineContext where TFrame : class
{
    /// <summary>
    /// Sends a new, already-built frame - fire-and-forget: a processor does not track or await the send it makes, so
    /// this returns nothing and the send proceeds in the background exactly as it would from any other caller (a
    /// failure is logged, not thrown back). Its frame ID, sender, and sent time are overwritten before
    /// it is routed, so only its content fields (body, addresses, ...) need to be set. A frame is shown to the user and stored only when the message handler recognizes it (see <see cref="IFrameBuilder{TFrame}.Message{THandler}"/>), so create a frame meant to be read through that handler.
    /// </summary>
    /// <param name="frame">What to send.</param>
    void Send(TFrame frame);
}
