namespace BlueHeighliner.Comlink;

/// <summary>One connection's side of an initial exchange, as a <see cref="IInitialProcessor"/> sees it, with items as plain objects since the engine does not know the host's types at compile time.</summary>
internal interface IInitialSession
{
    /// <summary>Gets what is known about the connection.</summary>
    IConnectionInfo Connection { get; }

    /// <summary>Gets a snapshot of the engine.</summary>
    IEngineContext Engine { get; }

    /// <summary>Marks the connection fully connected as <paramref name="userName"/>, once everything queued before it has been sent.</summary>
    /// <param name="userName">The user on the other end.</param>
    void Connected(string userName);

    /// <summary>Drops the connection, once everything queued before it has been sent.</summary>
    void Disconnect();

    /// <summary>Queues an item to be sent over the connection, in order with the session's other calls.</summary>
    /// <param name="item">An instance of the exchange's type.</param>
    void Send(object item);
}

/// <summary>The engine's view of a host's initial frame or packet processor, with items as plain objects.</summary>
internal interface IInitialProcessor
{
    /// <summary>Gets the type of item the exchange carries.</summary>
    Type ItemType { get; }

    /// <summary>Gets how long the exchange may take.</summary>
    TimeSpan Timeout { get; }

    /// <summary>Called on both nodes when the connection has formed.</summary>
    /// <param name="session">Controls the connection.</param>
    void OnConnected(IInitialSession session);

    /// <summary>Called for each item received before the connection is marked connected.</summary>
    /// <param name="session">Controls the connection.</param>
    /// <param name="item">What arrived.</param>
    void OnReceived(IInitialSession session, object item);
}
