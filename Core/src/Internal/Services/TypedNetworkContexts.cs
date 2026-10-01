namespace BlueHeighliner.Comlink.Services;

/// <summary>Presents an <see cref="INetworkEngineContext"/> to a host's processor with its message type.</summary>
/// <typeparam name="TMessage">The host's message type.</typeparam>
/// <param name="core">The context being presented.</param>
internal class TypedNetworkContext<TMessage>(INetworkEngineContext core) : INetworkContext<TMessage> where TMessage : class
{
    /// <inheritdoc />
    public UserInfo CurrentUser => core.CurrentUser;

    /// <inheritdoc />
    public IEnumerable<UserInfo> Users => core.Users;

    /// <inheritdoc />
    public IEnumerable<UserInfo> ConnectedUsers => core.ConnectedUsers;

    /// <inheritdoc />
    public bool IsConnected(string userName) => core.IsConnected(userName);

    /// <inheritdoc />
    public void Send(TMessage message) => core.Send(message);
}

/// <summary>Presents an <see cref="INetworkUserContext"/> to <see cref="INetworkProcessor{TMessage}.OnConnected"/>.</summary>
/// <typeparam name="TMessage">The host's message type.</typeparam>
/// <param name="core">The context being presented.</param>
internal sealed class TypedNetworkConnectedContext<TMessage>(INetworkUserContext core) : TypedNetworkContext<TMessage>(core), INetworkConnectedContext<TMessage> where TMessage : class
{
    /// <inheritdoc />
    public string TargetUser => core.TargetUser;
}

/// <summary>Presents an <see cref="INetworkUserContext"/> to <see cref="INetworkProcessor{TMessage}.OnDisconnected"/>.</summary>
/// <typeparam name="TMessage">The host's message type.</typeparam>
/// <param name="core">The context being presented.</param>
internal sealed class TypedNetworkDisconnectedContext<TMessage>(INetworkUserContext core) : TypedNetworkContext<TMessage>(core), INetworkDisconnectedContext<TMessage> where TMessage : class
{
    /// <inheritdoc />
    public string TargetUser => core.TargetUser;
}

/// <summary>Presents an <see cref="INetworkMessageContext"/> to <see cref="INetworkProcessor{TMessage}.OnReceived"/>.</summary>
/// <typeparam name="TMessage">The host's message type.</typeparam>
/// <param name="core">The context being presented.</param>
internal sealed class TypedNetworkReceivedContext<TMessage>(INetworkMessageContext core) : TypedNetworkContext<TMessage>(core), INetworkReceivedContext<TMessage> where TMessage : class
{
    /// <inheritdoc />
    public TMessage Message => (TMessage)core.Message;
}
