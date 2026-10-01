namespace BlueHeighliner.Comlink.Control;

/// <summary>Handed to <see cref="INetworkProcessor{TMessage}.OnDisconnected"/>.</summary>
/// <typeparam name="TMessage">The host's message type.</typeparam>
public interface INetworkDisconnectedContext<TMessage> : INetworkContext<TMessage> where TMessage : class
{
    /// <summary>Gets the user that disconnected.</summary>
    string TargetUser { get; }
}
