namespace BlueHeighliner.Comlink;

/// <summary>
/// Handed to an <see cref="IInitialFrameProcessor{TFrame}"/> for one connection that has just formed, to carry out the initial frame exchange on it:
/// send frames, then either mark the connection fully connected as a named user or disconnect it. Until one of those happens the connection is unusable,
/// and it is dropped if it takes too long.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface IInitialFrameContext<TFrame> : IEngineContext where TFrame : class
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

    /// <summary>Sends <paramref name="frame"/> over the connection in the background, in order with this context's other calls (a <see cref="Connected"/> after a send takes effect once it has been sent), and dropping the connection if it cannot be sent, serialized with the frame serializer and, when packets are configured, split into packets like any frame. It is not stored, routed or shown.</summary>
    /// <param name="frame">What to send.</param>
    void Send(TFrame frame);
}
