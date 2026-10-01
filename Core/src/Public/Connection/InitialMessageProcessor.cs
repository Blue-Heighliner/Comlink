namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// Carries out an initial message exchange on every new connection, so nodes can introduce themselves with messages: the host decides what to send, what
/// to make of what arrives, and when the connection counts as connected and as which user. Everything sent is a serialized instance of the message type,
/// and nothing else crosses the connection, so what the other node sends is recognized by position: once a connection has formed, each message a node
/// receives before it is marked connected is given to <see cref="OnInitial"/> on the accepting node or <see cref="OnReply"/> on the opening node. Every node on a
/// network must be configured alike. State one with <see cref="IMessageBuilder{TMessage}.InitialProcessor"/>.
/// </summary>
/// <typeparam name="TMessage">The host's message type.</typeparam>
public interface IInitialMessageProcessor<TMessage> where TMessage : class
{
    /// <summary>Called on both nodes when the connection has formed, before anything has been received. The opening node (<see cref="IInitialMessageContext{TMessage}.IsOpener"/>) usually sends its initial message here.</summary>
    /// <param name="context">Controls the connection.</param>
    Task OnConnected(IInitialMessageContext<TMessage> context);

    /// <summary>Called on the accepting node for each message the opening node sent, while the connection is not yet marked connected.</summary>
    /// <param name="context">Controls the connection.</param>
    /// <param name="message">What arrived.</param>
    Task OnInitial(IInitialMessageContext<TMessage> context, TMessage message);

    /// <summary>Called on the opening node for each message the accepting node sent, while the connection is not yet marked connected.</summary>
    /// <param name="context">Controls the connection.</param>
    /// <param name="message">What arrived.</param>
    Task OnReply(IInitialMessageContext<TMessage> context, TMessage message);
}
