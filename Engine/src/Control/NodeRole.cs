namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>
/// The networking topology role a running instance takes on. See <c>Docs/Peer.md</c> for the full
/// description of each role's connection and routing behavior.
/// </summary>
public enum NodeRole
{
    /// <summary>Direct peer-to-peer networking: every user connects straight to every other user it addresses. The default.</summary>
    Peer,
    /// <summary>Hierarchical networking: all traffic flows through one long-term connection to a configured server (<see cref="IEngineController.ServerEndpoint"/>).</summary>
    Client,
    /// <summary>Hierarchical networking: routes messages between its own child clients and other servers (<see cref="IEngineController.Servers"/>).</summary>
    Server
}
