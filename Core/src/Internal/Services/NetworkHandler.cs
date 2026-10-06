namespace BlueHeighliner.Comlink;

/// <summary>The engine's view of a host's <see cref="INetworkProcessor{TFrame}"/>, with messages as plain objects since the engine does not know the host's type at compile time.</summary>
internal interface INetworkHandler
{
    /// <summary>Asked once when the engine starts, in a client or relay, whether the engine keeps the network indicator up to date itself.</summary>
    /// <param name="context">A snapshot of the engine.</param>
    bool UseAutomaticNetworkIndicator(IEngineContext context);

    /// <summary>Called when a user goes from having no live peer connection to having at least one.</summary>
    /// <param name="context">A snapshot of the engine taken for this event.</param>
    void OnConnected(INetworkUserContext context);

    /// <summary>Called when a user goes from having at least one live peer connection to having none.</summary>
    /// <param name="context">A snapshot of the engine taken for this event.</param>
    void OnDisconnected(INetworkUserContext context);

    /// <summary>Called whenever this instance receives a new (non-receipt) message from a peer.</summary>
    /// <param name="context">A snapshot of the engine taken for this event.</param>
    void OnReceived(INetworkFrameContext context);
}
