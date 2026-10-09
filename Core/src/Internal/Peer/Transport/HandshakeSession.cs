namespace BlueHeighliner.Comlink;

/// <summary>One connection's side of a handshake, as a <see cref="IHandshakeHandler"/> sees it, with items as plain objects since the engine does not know the host's types at compile time.</summary>
internal interface IHandshakeSession
{
    /// <summary>Gets what is known about the connection.</summary>
    IConnectionInfo Connection { get; }

    /// <summary>Gets a snapshot of the engine.</summary>
    IEngineContext Engine { get; }

    /// <summary>Marks the connection fully connected as <paramref name="userName"/>, after everything awaited before it has been sent.</summary>
    /// <param name="userName">The user on the other end.</param>
    Task Connected(string userName);

    /// <summary>Drops the connection.</summary>
    Task Disconnect();

    /// <summary>Sends an item over the connection, completing once the other node has accepted it. A send that fails drops the connection.</summary>
    /// <param name="item">An instance of the handshake's type.</param>
    Task<bool> Send(object item);
}
