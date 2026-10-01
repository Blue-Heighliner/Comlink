namespace BlueHeighliner.Comlink.Control;

/// <summary>Handed to <see cref="INetworkProcessor{TMessage}.OnConnected"/>.</summary>
/// <typeparam name="TMessage">The host's message type.</typeparam>
public interface INetworkConnectedContext<TMessage> : INetworkContext<TMessage> where TMessage : class
{
    /// <summary>Gets the user that connected.</summary>
    string TargetUser { get; }
}
