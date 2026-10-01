namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// Handed to an <see cref="IInitialMessageProcessor{TMessage}"/> for one connection that has just formed, to carry out the initial message exchange on it:
/// send messages, then either mark the connection fully connected as a named user or disconnect it. Until one of those happens the connection is unusable,
/// and it is dropped if it takes too long.
/// </summary>
/// <typeparam name="TMessage">The host's message type.</typeparam>
public interface IInitialMessageContext<TMessage> : IEngineContext where TMessage : class
{
    /// <summary>Gets a value indicating whether this node opens the exchange: the node that opened the connection, or for a serial link, which both ends open, the node at the higher station address.</summary>
    bool IsOpener { get; }

    /// <summary>Gets what is known about the connection.</summary>
    IConnectionInfo Connection { get; }

    /// <summary>Marks the connection fully connected, as <paramref name="userName"/>. The connection is then usable, and anything after it is ordinary traffic.</summary>
    /// <param name="userName">The user on the other end.</param>
    void Connected(string userName);

    /// <summary>Drops the connection.</summary>
    void Disconnect();

    /// <summary>Sends <paramref name="message"/> over the connection, serialized with the message serializer and, when packets are configured, split into packets like any message. It is not stored, routed or shown.</summary>
    /// <param name="message">What to send.</param>
    /// <returns>Whether the message was accepted for sending.</returns>
    Task<bool> Send(TMessage message);
}
