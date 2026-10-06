namespace BlueHeighliner.Comlink;

/// <summary>What every network processor context knows, with frames as plain objects since the engine does not know the host's frame type at compile time. The host's processor sees it through <see cref="INetworkContext{TFrame}"/>.</summary>
internal interface INetworkEngineContext : IEngineContext
{
    /// <summary>Sends a new, already-built frame in the background.</summary>
    /// <param name="frame">An instance of the configured frame type.</param>
    /// <exception cref="ArgumentException"><paramref name="frame"/> is not an instance of the configured frame type.</exception>
    void Send(object frame);
    /// <summary>Sets the network indicator online or offline.</summary>
    /// <param name="isOnline"><see langword="true"/> for online.</param>
    void SetNetworkIndicator(bool isOnline);
}

/// <summary>An <see cref="INetworkEngineContext"/> for a user connecting or disconnecting.</summary>
internal interface INetworkUserContext : INetworkEngineContext
{
    /// <summary>The user that connected or disconnected.</summary>
    string TargetUser { get; }
}

/// <summary>An <see cref="INetworkEngineContext"/> for a message being received.</summary>
internal interface INetworkFrameContext : INetworkEngineContext
{
    /// <summary>The frame that was received, an instance of the configured frame type.</summary>
    object Frame { get; }
}

/// <summary>Shared <see cref="INetworkEngineContext"/> implementation for <see cref="NetworkUserContext"/> and <see cref="NetworkFrameContext"/>.</summary>
internal abstract class NetworkEngineContext : INetworkEngineContext
{
    /// <summary>Initializes the shared state of a new network context.</summary>
    /// <param name="engine">The engine snapshot the context exposes.</param>
    /// <param name="engineController">Validates <see cref="Send"/> against the configured frame type.</param>
    /// <param name="messageRouting">Routes a <see cref="Send"/> call on <see cref="CurrentUser"/>'s behalf.</param>
    /// <param name="logger">Logs a failed <see cref="Send"/>, since it is fire-and-forget and nothing else observes its outcome.</param>
    /// <param name="indicator">Set by <see cref="SetNetworkIndicator"/>.</param>
    protected NetworkEngineContext(IEngineContext engine, IEngineController engineController, IMessageRoutingService messageRouting, INetworkIndicator indicator, ILogger logger)
    {
        this.indicator = indicator;
        this.engine = engine;
        this.engineController = engineController;
        this.messageRouting = messageRouting;
        this.logger = logger;
    }

    private readonly IEngineContext engine;
    private readonly IEngineController engineController;
    private readonly IMessageRoutingService messageRouting;
    private readonly INetworkIndicator indicator;
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
    public void SetNetworkIndicator(bool isOnline) => indicator.Set(isOnline);

    /// <inheritdoc />
    public void Send(object frame)
    {
        if (frame.GetType() != engineController.FrameType)
        {
            throw new ArgumentException($"Must be an instance of the configured frame type ({engineController.FrameType}).", nameof(frame));
        }

        _ = SendInBackground(frame);
    }

    private async Task SendInBackground(object frame)
    {
        try { await messageRouting.RouteFrame(CurrentUser.Name, frame, CancellationToken.None); }
        catch (Exception ex) { logger.LogError(ex, "A processor-originated frame send failed"); }
    }
}

/// <inheritdoc cref="INetworkUserContext" />
internal sealed class NetworkUserContext : NetworkEngineContext, INetworkUserContext
{
    /// <summary>Initializes a new <see cref="NetworkUserContext"/>.</summary>
    /// <param name="engine">The engine snapshot the context exposes.</param>
    /// <param name="targetUser">The user that connected or disconnected.</param>
    /// <param name="engineController">Validates <see cref="NetworkEngineContext.Send"/> against the configured frame type.</param>
    /// <param name="messageRouting">Routes a <see cref="NetworkEngineContext.Send"/> call on the current user's behalf.</param>
    /// <param name="logger">Logs a failed send, since it is fire-and-forget.</param>
    /// <param name="indicator">Set by <see cref="NetworkEngineContext.SetNetworkIndicator"/>.</param>
    public NetworkUserContext(IEngineContext engine, string targetUser, IEngineController engineController, IMessageRoutingService messageRouting, INetworkIndicator indicator, ILogger logger)
        : base(engine, engineController, messageRouting, indicator, logger)
    {
        TargetUser = targetUser;
    }

    /// <inheritdoc />
    public string TargetUser { get; }
}

/// <inheritdoc cref="INetworkFrameContext" />
internal sealed class NetworkFrameContext : NetworkEngineContext, INetworkFrameContext
{
    /// <summary>Initializes a new <see cref="NetworkFrameContext"/>.</summary>
    /// <param name="engine">The engine snapshot the context exposes.</param>
    /// <param name="frame">The frame that was received (an instance of the configured frame type).</param>
    /// <param name="engineController">Validates <see cref="NetworkEngineContext.Send"/> against the configured frame type.</param>
    /// <param name="messageRouting">Routes a <see cref="NetworkEngineContext.Send"/> call on the current user's behalf.</param>
    /// <param name="logger">Logs a failed send, since it is fire-and-forget.</param>
    /// <param name="indicator">Set by <see cref="NetworkEngineContext.SetNetworkIndicator"/>.</param>
    public NetworkFrameContext(IEngineContext engine, object frame, IEngineController engineController, IMessageRoutingService messageRouting, INetworkIndicator indicator, ILogger logger)
        : base(engine, engineController, messageRouting, indicator, logger)
    {
        Frame = frame;
    }

    /// <inheritdoc />
    public object Frame { get; }
}
