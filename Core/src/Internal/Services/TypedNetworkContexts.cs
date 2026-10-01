namespace BlueHeighliner.Comlink.Services;

/// <summary>Presents an <see cref="INetworkEngineContext"/> to a host's processor with its frame type.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <param name="core">The context being presented.</param>
internal class TypedNetworkContext<TFrame>(INetworkEngineContext core) : INetworkContext<TFrame> where TFrame : class
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
    public void Send(TFrame frame) => core.Send(frame);
}

/// <summary>Presents an <see cref="INetworkUserContext"/> to <see cref="INetworkProcessor{TFrame}.OnConnected"/>.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <param name="core">The context being presented.</param>
internal sealed class TypedNetworkConnectedContext<TFrame>(INetworkUserContext core) : TypedNetworkContext<TFrame>(core), INetworkConnectedContext<TFrame> where TFrame : class
{
    /// <inheritdoc />
    public string TargetUser => core.TargetUser;
}

/// <summary>Presents an <see cref="INetworkUserContext"/> to <see cref="INetworkProcessor{TFrame}.OnDisconnected"/>.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <param name="core">The context being presented.</param>
internal sealed class TypedNetworkDisconnectedContext<TFrame>(INetworkUserContext core) : TypedNetworkContext<TFrame>(core), INetworkDisconnectedContext<TFrame> where TFrame : class
{
    /// <inheritdoc />
    public string TargetUser => core.TargetUser;
}

/// <summary>Presents an <see cref="INetworkFrameContext"/> to <see cref="INetworkProcessor{TFrame}.OnReceived"/>.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <param name="core">The context being presented.</param>
internal sealed class TypedNetworkReceivedContext<TFrame>(INetworkFrameContext core) : TypedNetworkContext<TFrame>(core), INetworkReceivedContext<TFrame> where TFrame : class
{
    /// <inheritdoc />
    public TFrame Frame => (TFrame)core.Frame;
}
