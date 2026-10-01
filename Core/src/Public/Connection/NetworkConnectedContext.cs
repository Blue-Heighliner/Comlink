namespace BlueHeighliner.Comlink.Control;

/// <summary>Handed to <see cref="INetworkProcessor{TFrame}.OnConnected"/>.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface INetworkConnectedContext<TFrame> : INetworkContext<TFrame> where TFrame : class
{
    /// <summary>Gets the user that connected.</summary>
    string TargetUser { get; }
}
