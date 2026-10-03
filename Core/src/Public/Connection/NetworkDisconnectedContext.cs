namespace BlueHeighliner.Comlink;

/// <summary>Handed to <see cref="INetworkProcessor{TFrame}.OnDisconnected"/>.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface INetworkDisconnectedContext<TFrame> : INetworkContext<TFrame> where TFrame : class
{
    /// <summary>Gets the user that disconnected.</summary>
    string TargetUser { get; }
}
