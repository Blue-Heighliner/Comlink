namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// Runs host code in reaction to peer activity, independent of any UI. Each method is handed a simplified snapshot of the engine (see
/// <see cref="IEngineContext"/>) that can also originate new messages. A method runs in the background: it is not awaited
/// by the engine, and an exception it throws is logged rather than thrown back. State one with <see cref="IMessageBuilder{TMessage}.Processor"/>.
/// </summary>
/// <typeparam name="TMessage">The host's message type.</typeparam>
public interface INetworkProcessor<TMessage> where TMessage : class
{
    /// <summary>Called when a user goes from having no live peer connection to having at least one. <see cref="INetworkConnectedContext{TMessage}.TargetUser"/> names the user that connected.</summary>
    /// <param name="context">A snapshot of the engine taken for this event.</param>
    Task OnConnected(INetworkConnectedContext<TMessage> context);

    /// <summary>Called when a user goes from having at least one live peer connection to having none. <see cref="INetworkDisconnectedContext{TMessage}.TargetUser"/> names the user that disconnected.</summary>
    /// <param name="context">A snapshot of the engine taken for this event.</param>
    Task OnDisconnected(INetworkDisconnectedContext<TMessage> context);

    /// <summary>Called whenever this instance receives a new (non-confirmation) message from a peer. <see cref="INetworkReceivedContext{TMessage}.Message"/> carries it.</summary>
    /// <param name="context">A snapshot of the engine taken for this event.</param>
    Task OnReceived(INetworkReceivedContext<TMessage> context);
}
