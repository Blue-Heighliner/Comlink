namespace BlueHeighliner.Comlink.Services;

/// <summary>What every network processor context knows, with messages as plain objects since the engine does not know the host's message type at compile time. The host's processor sees it through <see cref="INetworkContext{TMessage}"/>.</summary>
internal interface INetworkEngineContext : IEngineContext
{
    /// <summary>Sends a new, already-built message in the background.</summary>
    /// <param name="message">An instance of the configured message type.</param>
    /// <exception cref="ArgumentException"><paramref name="message"/> is not an instance of the configured message type.</exception>
    void Send(object message);
}

/// <summary>An <see cref="INetworkEngineContext"/> for a user connecting or disconnecting.</summary>
internal interface INetworkUserContext : INetworkEngineContext
{
    /// <summary>The user that connected or disconnected.</summary>
    string TargetUser { get; }
}

/// <summary>An <see cref="INetworkEngineContext"/> for a message being received.</summary>
internal interface INetworkMessageContext : INetworkEngineContext
{
    /// <summary>The message that was received, an instance of the configured message type.</summary>
    object Message { get; }
}

/// <summary>Shared <see cref="INetworkEngineContext"/> implementation for <see cref="NetworkUserContext"/> and <see cref="NetworkMessageContext"/>.</summary>
internal abstract class NetworkEngineContext : INetworkEngineContext
{
    /// <summary>Initializes the shared state of a new network context.</summary>
    /// <param name="engine">The engine snapshot the context exposes.</param>
    /// <param name="engineController">Validates <see cref="Send"/> against the configured message type.</param>
    /// <param name="messageRouting">Routes a <see cref="Send"/> call on <see cref="CurrentUser"/>'s behalf.</param>
    /// <param name="logger">Logs a failed <see cref="Send"/>, since it is fire-and-forget and nothing else observes its outcome.</param>
    protected NetworkEngineContext(IEngineContext engine, IEngineController engineController, IMessageRoutingService messageRouting, ILogger logger)
    {
        this.engine = engine;
        this.engineController = engineController;
        this.messageRouting = messageRouting;
        this.logger = logger;
    }

    private readonly IEngineContext engine;
    private readonly IEngineController engineController;
    private readonly IMessageRoutingService messageRouting;
    private readonly ILogger logger;

    /// <inheritdoc />
    public UserInfo CurrentUser => engine.CurrentUser;

    /// <inheritdoc />
    public IEnumerable<UserInfo> Users => engine.Users;

    /// <inheritdoc />
    public IEnumerable<UserInfo> ConnectedUsers => engine.ConnectedUsers;

    /// <inheritdoc />
    public bool IsConnected(string userName) => engine.IsConnected(userName);

    /// <inheritdoc />
    public void Send(object message)
    {
        if (message.GetType() != engineController.MessageType)
        {
            throw new ArgumentException($"Must be an instance of the configured message type ({engineController.MessageType}).", nameof(message));
        }

        _ = SendInBackground(message);
    }

    private async Task SendInBackground(object message)
    {
        try { await messageRouting.RouteMessage(CurrentUser.Name, message, CancellationToken.None); }
        catch (Exception ex) { logger.LogError(ex, "A processor-originated message send failed"); }
    }
}

/// <inheritdoc cref="INetworkUserContext" />
internal sealed class NetworkUserContext : NetworkEngineContext, INetworkUserContext
{
    /// <summary>Initializes a new <see cref="NetworkUserContext"/>.</summary>
    /// <param name="engine">The engine snapshot the context exposes.</param>
    /// <param name="targetUser">The user that connected or disconnected.</param>
    /// <param name="engineController">Validates <see cref="NetworkEngineContext.Send"/> against the configured message type.</param>
    /// <param name="messageRouting">Routes a <see cref="NetworkEngineContext.Send"/> call on the current user's behalf.</param>
    /// <param name="logger">Logs a failed send, since it is fire-and-forget.</param>
    public NetworkUserContext(IEngineContext engine, string targetUser, IEngineController engineController, IMessageRoutingService messageRouting, ILogger logger)
        : base(engine, engineController, messageRouting, logger)
    {
        TargetUser = targetUser;
    }

    /// <inheritdoc />
    public string TargetUser { get; }
}

/// <inheritdoc cref="INetworkMessageContext" />
internal sealed class NetworkMessageContext : NetworkEngineContext, INetworkMessageContext
{
    /// <summary>Initializes a new <see cref="NetworkMessageContext"/>.</summary>
    /// <param name="engine">The engine snapshot the context exposes.</param>
    /// <param name="message">The message that was received (an instance of the configured message type).</param>
    /// <param name="engineController">Validates <see cref="NetworkEngineContext.Send"/> against the configured message type.</param>
    /// <param name="messageRouting">Routes a <see cref="NetworkEngineContext.Send"/> call on the current user's behalf.</param>
    /// <param name="logger">Logs a failed send, since it is fire-and-forget.</param>
    public NetworkMessageContext(IEngineContext engine, object message, IEngineController engineController, IMessageRoutingService messageRouting, ILogger logger)
        : base(engine, engineController, messageRouting, logger)
    {
        Message = message;
    }

    /// <inheritdoc />
    public object Message { get; }
}
