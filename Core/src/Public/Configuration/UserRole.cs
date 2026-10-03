namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// The networking topology role a running instance takes on. See <c>Docs/Components/Peer.md</c> for the full
/// description of each role's connection and routing behavior.
/// </summary>
public enum UserRole
{
    /// <summary>Direct peer-to-peer networking: every user connects straight to every other user it addresses. The default.</summary>
    Peer,
    /// <summary>Hierarchical networking: all traffic flows through one long-term connection to a server, its <see cref="UserInfo.Parent"/>.</summary>
    Client,
    /// <summary>Hierarchical networking: routes messages between its own children (each server user's <see cref="UserInfo.Children"/>) and other servers, over the connections its links form: the children and other servers connect to it, or it connects to them, as each link's mode says.</summary>
    Server,
    /// <summary>
    /// Hierarchical networking: sits between clients and a server as a direct network path. It forwards everything its <see cref="UserInfo.Children"/> send to the server it is linked to (its <see cref="UserInfo.Parent"/>),
    /// and everything that server sends to whichever of those clients it addresses, unmodified and without receipting or storing anything. The server lists the relay among its own <see cref="UserInfo.Children"/>.
    /// </summary>
    Relay
}
