namespace BlueHeighliner.Comlink;

/// <summary>The engine's view of a host's handshake handler, with items as plain objects.</summary>
internal interface IHandshakeHandler
{
    /// <summary>Gets the type of item the handshake carries.</summary>
    Type ItemType { get; }

    /// <summary>Gets how long the handshake may take.</summary>
    TimeSpan Timeout { get; }

    /// <summary>Called on both nodes when the connection has formed.</summary>
    /// <param name="session">Controls the connection.</param>
    Task OnConnected(IHandshakeSession session);

    /// <summary>Called for each item received before the connection is marked connected.</summary>
    /// <param name="session">Controls the connection.</param>
    /// <param name="item">What arrived.</param>
    Task OnReceived(IHandshakeSession session, object item);
}
