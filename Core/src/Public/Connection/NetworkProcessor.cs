namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// Runs host code in reaction to peer activity, independent of any UI. Each method is handed a simplified snapshot of the engine (see
/// <see cref="IEngineContext"/>) that can also originate new frames. A method runs in the background: it is not awaited
/// by the engine, and an exception it throws is logged rather than thrown back. State one with <see cref="IFrameBuilder{TFrame}.Processor"/>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface INetworkProcessor<TFrame> where TFrame : class
{
    /// <summary>Called when a user goes from having no live peer connection to having at least one. <see cref="INetworkConnectedContext{TFrame}.TargetUser"/> names the user that connected.</summary>
    /// <param name="context">A snapshot of the engine taken for this event.</param>
    Task OnConnected(INetworkConnectedContext<TFrame> context);

    /// <summary>Called when a user goes from having at least one live peer connection to having none. <see cref="INetworkDisconnectedContext{TFrame}.TargetUser"/> names the user that disconnected.</summary>
    /// <param name="context">A snapshot of the engine taken for this event.</param>
    Task OnDisconnected(INetworkDisconnectedContext<TFrame> context);

    /// <summary>Called whenever this instance receives a new (non-receipt) frame from a peer, whether or not it is a message. <see cref="INetworkReceivedContext{TFrame}.Frame"/> carries it.</summary>
    /// <param name="context">A snapshot of the engine taken for this event.</param>
    Task OnReceived(INetworkReceivedContext<TFrame> context);
}
