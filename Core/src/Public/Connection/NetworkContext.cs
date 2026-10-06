namespace BlueHeighliner.Comlink;

/// <summary>What every <see cref="INetworkProcessor{TFrame}"/> method is handed: the engine, and a way to originate a new frame.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface INetworkContext<TFrame> : IEngineContext where TFrame : class
{
    /// <summary>
    /// Sends a new, already-built frame - fire-and-forget: a processor does not track or await the send it makes, so
    /// this returns nothing and the send proceeds in the background exactly as it would from any other caller (a
    /// failure is logged, not thrown back). Its frame ID, sender, and sent time are overwritten before
    /// it is routed, so only its content fields (body, addresses, ...) need to be set. A frame is shown to the user and stored only when the message handler recognizes it (see <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.Message{THandler}"/>), so create a frame meant to be read through that handler.
    /// </summary>
    /// <param name="frame">What to send.</param>
    void Send(TFrame frame);

    /// <summary>
    /// Sets the network indicator in the top bar of a client or relay to online or offline, with the labels and colors the display handler states. It only sticks while the processor has taken the indicator over (see <see cref="INetworkProcessor{TFrame}.UseAutomaticNetworkIndicator"/>);
    /// otherwise the engine sets it itself from the connection to the parent and overwrites what is set here the next time that connection changes.
    /// </summary>
    /// <param name="isOnline"><see langword="true"/> to show online, <see langword="false"/> to show offline.</param>
    void SetNetworkIndicator(bool isOnline);
}
