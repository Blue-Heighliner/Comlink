namespace BlueHeighliner.Comlink;

/// <summary>One connection's side of a handshake, as a <see cref="IHandshakeHandler"/> sees it, with items as plain objects since the engine does not know the host's types at compile time.</summary>
internal interface IHandshakeSession
{
    /// <summary>Gets what is known about the connection.</summary>
    IConnectionInfo Connection { get; }

    /// <summary>Accepts the other node: marks the connection fully connected as <paramref name="userName"/>, after everything awaited before it has been sent.</summary>
    /// <param name="userName">The user on the other end.</param>
    Task Accept(string userName);

    /// <summary>Rejects the other node: drops the connection and logs <paramref name="reason"/> as why.</summary>
    /// <param name="reason">Why the connection is refused.</param>
    Task Reject(string reason);

    /// <summary>Sends an item over the connection, completing once the other node has accepted it. A send that fails drops the connection.</summary>
    /// <param name="item">An instance of the handshake's type.</param>
    Task<bool> Send(object item);
}
