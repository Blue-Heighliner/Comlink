namespace BlueHeighliner.Comlink;

/// <summary>Handed to <see cref="INetworkProcessor{TFrame}.OnReceived"/>.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface INetworkReceivedContext<TFrame> : INetworkContext<TFrame> where TFrame : class
{
    /// <summary>Gets the frame that was received.</summary>
    TFrame Frame { get; }
}
