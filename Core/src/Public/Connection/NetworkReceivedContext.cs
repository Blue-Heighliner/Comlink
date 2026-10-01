namespace BlueHeighliner.Comlink.Control;

/// <summary>Handed to <see cref="INetworkProcessor{TMessage}.OnReceived"/>.</summary>
/// <typeparam name="TMessage">The host's message type.</typeparam>
public interface INetworkReceivedContext<TMessage> : INetworkContext<TMessage> where TMessage : class
{
    /// <summary>Gets the message that was received.</summary>
    TMessage Message { get; }
}
