namespace BlueHeighliner.Comlink;

/// <summary>
/// Handed to an <see cref="IFrameHandshakeHandler{TFrame}"/> for one connection that has just formed, to carry out the handshake of frames on it:
/// send frames, then either mark the connection fully connected as a named user or disconnect it. Until one of those happens the connection is unusable,
/// and it is dropped if it takes too long.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface IFrameHandshakeContext<TFrame> : IEngineContext where TFrame : class
{
    /// <summary>Gets what is known about the connection.</summary>
    IConnectionInfo Connection { get; }

    /// <summary>Marks the connection fully connected, as <paramref name="userName"/>. The connection is then usable, and anything after it is ordinary traffic.</summary>
    /// <param name="userName">The user on the other end.</param>
    Task Connected(string userName);

    /// <summary>Drops the connection.</summary>
    Task Disconnect();

    /// <summary>Sends <paramref name="frame"/> over the connection and completes once the other node has accepted it, so a handler awaits it before calling <see cref="Connected"/>. The connection is dropped if it cannot be sent, serialized with the frame serializer and, when packets are configured, split into packets like any frame. It is not stored, routed or shown.</summary>
    /// <param name="frame">What to send.</param>
    /// <returns>Whether the other node accepted it.</returns>
    Task<bool> Send(TFrame frame);
}
