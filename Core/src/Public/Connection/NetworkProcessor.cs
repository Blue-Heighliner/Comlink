namespace BlueHeighliner.Comlink;

/// <summary>
/// Runs host code in reaction to peer activity, independent of any UI. Each method is handed a simplified snapshot of the engine (see
/// <see cref="IEngineContext"/>) that can also originate new frames. A method runs in the background: it is not awaited
/// by the engine, and an exception it throws is logged rather than thrown back. State one with <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Processor"/>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface INetworkProcessor<TFrame> where TFrame : class
{
    /// <summary>Called when a user goes from having no live peer connection to having at least one. <see cref="INetworkConnectedContext{TFrame}.TargetUser"/> names the user that connected.</summary>
    /// <param name="context">A snapshot of the engine taken for this event.</param>
    void OnConnected(INetworkConnectedContext<TFrame> context);

    /// <summary>Called when a user goes from having at least one live peer connection to having none. <see cref="INetworkDisconnectedContext{TFrame}.TargetUser"/> names the user that disconnected.</summary>
    /// <param name="context">A snapshot of the engine taken for this event.</param>
    void OnDisconnected(INetworkDisconnectedContext<TFrame> context);

    /// <summary>
    /// Asked once when the engine starts, in a client or relay, whether the engine keeps the network indicator in the top bar up to date itself: by default it shows online while this node is directly connected to its parent (for a client whose parent is a relay,
    /// its connection to the relay), and offline otherwise. Return <see langword="false"/> to take that over, after which the indicator only changes when the processor calls <see cref="INetworkContext{TFrame}.SetNetworkIndicator"/>. The default keeps the automatic behavior.
    /// </summary>
    /// <param name="context">A snapshot of the engine.</param>
    bool UseAutomaticNetworkIndicator(IEngineContext context) => true;

    /// <summary>Called whenever this instance receives a new (non-receipt) frame from a peer, whether or not it is a message. <see cref="INetworkReceivedContext{TFrame}.Frame"/> carries it.</summary>
    /// <param name="context">A snapshot of the engine taken for this event.</param>
    void OnReceived(INetworkReceivedContext<TFrame> context);
}
