namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>One connection's side of an initial exchange, as a <see cref="IInitialProcessor"/> sees it, with items as plain objects since the engine does not know the host's types at compile time.</summary>
internal interface IInitialSession
{
    /// <summary>Gets a value indicating whether this node opens the exchange.</summary>
    bool IsOpener { get; }

    /// <summary>Gets what is known about the connection.</summary>
    IConnectionInfo Connection { get; }

    /// <summary>Gets a snapshot of the engine.</summary>
    IEngineContext Engine { get; }

    /// <summary>Marks the connection fully connected as <paramref name="userName"/>.</summary>
    /// <param name="userName">The user on the other end.</param>
    void Connected(string userName);

    /// <summary>Drops the connection.</summary>
    void Disconnect();

    /// <summary>Sends an item over the connection.</summary>
    /// <param name="item">An instance of the exchange's type.</param>
    /// <returns>Whether it was accepted for sending.</returns>
    Task<bool> Send(object item);
}

/// <summary>The engine's view of a host's initial message or packet processor, with items as plain objects.</summary>
internal interface IInitialProcessor
{
    /// <summary>Gets the type of item the exchange carries.</summary>
    Type ItemType { get; }

    /// <summary>Called on both nodes when the connection has formed.</summary>
    /// <param name="session">Controls the connection.</param>
    Task OnConnected(IInitialSession session);

    /// <summary>Called on the accepting node for an item the opening node sent.</summary>
    /// <param name="session">Controls the connection.</param>
    /// <param name="item">What arrived.</param>
    Task OnInitial(IInitialSession session, object item);

    /// <summary>Called on the opening node for an item the accepting node sent.</summary>
    /// <param name="session">Controls the connection.</param>
    /// <param name="item">What arrived.</param>
    Task OnReply(IInitialSession session, object item);
}
