namespace BlueHeighliner.Comlink.Services;

/// <summary>The engine's view of a host's <see cref="INetworkProcessor{TFrame}"/>, with messages as plain objects since the engine does not know the host's type at compile time.</summary>
internal interface INetworkHandler
{
    /// <summary>Called when a user goes from having no live peer connection to having at least one.</summary>
    /// <param name="context">A snapshot of the engine taken for this event.</param>
    Task OnConnected(INetworkUserContext context);

    /// <summary>Called when a user goes from having at least one live peer connection to having none.</summary>
    /// <param name="context">A snapshot of the engine taken for this event.</param>
    Task OnDisconnected(INetworkUserContext context);

    /// <summary>Called whenever this instance receives a new (non-receipt) message from a peer.</summary>
    /// <param name="context">A snapshot of the engine taken for this event.</param>
    Task OnReceived(INetworkFrameContext context);
}
