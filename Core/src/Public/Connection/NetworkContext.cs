namespace BlueHeighliner.Comlink.Control;

/// <summary>What every <see cref="INetworkProcessor{TMessage}"/> method is handed: the engine, and a way to originate a new message.</summary>
/// <typeparam name="TMessage">The host's message type.</typeparam>
public interface INetworkContext<TMessage> : IEngineContext where TMessage : class
{
    /// <summary>
    /// Sends a new, already-built message - fire-and-forget: a processor does not track or await the send it makes, so
    /// this returns nothing and the send proceeds in the background exactly as it would from any other caller (a
    /// failure is logged, not thrown back). Its message ID, sender, and sent time are overwritten before
    /// it is routed, so only its content fields (subject, body, addresses, ...) need to be set.
    /// </summary>
    /// <param name="message">What to send.</param>
    void Send(TMessage message);
}
