namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// The networking topology role a running instance takes on. See <c>Docs/Components/Peer.md</c> for the full
/// description of each role's connection and routing behavior.
/// </summary>
public enum NodeRole
{
    /// <summary>Direct peer-to-peer networking: every user connects straight to every other user it addresses. The default.</summary>
    Peer,
    /// <summary>Hierarchical networking: all traffic flows through one long-term connection to a server, the first of the points given to <see cref="IEngineBuilder.OutgoingPoint"/>.</summary>
    Client,
    /// <summary>Hierarchical networking: routes messages between its own child clients and other servers (<see cref="IEngineBuilder.Server"/>), over connections the clients and other servers open to it or it opens through <see cref="IEngineBuilder.OutgoingPoint"/>.</summary>
    Server
}
