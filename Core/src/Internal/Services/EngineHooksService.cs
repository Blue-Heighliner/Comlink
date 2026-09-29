namespace BlueHeighliner.Comlink.Services;

/// <summary>
/// Runs the host's connection and message hooks (<see cref="IEngineController.UserConnectedHooks"/>,
/// <see cref="IEngineController.UserDisconnectedHooks"/>, <see cref="IEngineController.MessageReceivedHooks"/>)
/// whenever <see cref="IPeerService"/> raises the matching event, in both Client and Headless mode. Every hook
/// firing for one event gets a single freshly-built context, so hooks handling the same firing see a consistent
/// snapshot even if a user connects or disconnects while they run.
/// </summary>
internal interface IEngineHooksService
{
    /// <summary>Subscribes to <see cref="IPeerService"/>'s connection and message events and blocks until <paramref name="cancellation"/> is cancelled.</summary>
    Task Start(CancellationToken cancellation);
}

/// <inheritdoc cref="IEngineHooksService" />
internal sealed class EngineHooksService : IEngineHooksService
{
    /// <summary>Initializes a new <see cref="EngineHooksService"/>.</summary>
    public EngineHooksService(
        IPeerService peerService,
        IEngineController engineController,
        IUserService userService,
        IMessageRoutingService messageRouting,
        ILoggerFactory loggerFactory)
    {
        this.peerService = peerService;
        this.engineController = engineController;
        this.userService = userService;
        this.messageRouting = messageRouting;
        logger = loggerFactory.CreateLogger("ACTIVITY");
    }

    private readonly IPeerService peerService;
    private readonly IEngineController engineController;
    private readonly IUserService userService;
    private readonly IMessageRoutingService messageRouting;
    private readonly ILogger logger;

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        if (engineController.UserConnectedHooks.Count == 0 && engineController.UserDisconnectedHooks.Count == 0 && engineController.MessageReceivedHooks.Count == 0)
        {
            return;
        }

        peerService.UserConnected += OnUserConnected;
        peerService.UserDisconnected += OnUserDisconnected;
        peerService.MessageDelivered += OnMessageDelivered;

        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
        finally
        {
            peerService.UserConnected -= OnUserConnected;
            peerService.UserDisconnected -= OnUserDisconnected;
            peerService.MessageDelivered -= OnMessageDelivered;
        }
    }

    private Task OnUserConnected(string userName)
    {
        RunHooks(engineController.UserConnectedHooks, () => BuildConnectionContext(userName), "OnUserConnected", userName);
        return Task.CompletedTask;
    }

    private Task OnUserDisconnected(string userName)
    {
        RunHooks(engineController.UserDisconnectedHooks, () => BuildConnectionContext(userName), "OnUserDisconnected", userName);
        return Task.CompletedTask;
    }

    private Task OnMessageDelivered(object payload)
    {
        RunHooks(engineController.MessageReceivedHooks, () => BuildMessageContext(payload), "OnMessageReceived", engineController.GetMessageId(payload));
        return Task.CompletedTask;
    }

    private void RunHooks<TContext>(IReadOnlyList<Action<TContext>> hooks, Func<TContext> buildContext, string hookName, string subject)
    {
        if (hooks.Count == 0) { return; }

        TContext context = buildContext();
        foreach (Action<TContext> hook in hooks)
        {
            try { hook(context); }
            catch (Exception ex) { logger.LogError(ex, "A {HookName} hook failed for {Subject}", hookName, subject); }
        }
    }

    private IUserConnectionHookContext BuildConnectionContext(string targetUser)
        => new UserConnectionHookContext(RequireCurrentUser(), engineController.Users, engineController.UserGroups, peerService.IsUserConnected, targetUser, engineController, messageRouting, peerService, logger);

    private IMessageReceivedHookContext BuildMessageContext(object message)
        => new MessageReceivedHookContext(RequireCurrentUser(), engineController.Users, engineController.UserGroups, peerService.IsUserConnected, message, engineController, messageRouting, peerService, logger);

    private UserInfo RequireCurrentUser()
        => userService.GetCurrentUserInfo()
            ?? throw new InvalidOperationException("A hook fired with no installed user, which should never happen: EngineHooksService.Start only ever runs once one is installed.");
}
