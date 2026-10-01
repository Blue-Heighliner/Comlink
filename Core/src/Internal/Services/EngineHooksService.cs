namespace BlueHeighliner.Comlink.Services;

/// <summary>
/// Runs the host's <see cref="IEngineController.NetworkHandler"/> whenever <see cref="IPeerService"/> raises the
/// matching event, in both Client and Headless mode. Each event gets a single freshly-built context, so the
/// processor sees a consistent snapshot even if a user connects or disconnects while it runs.
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
        if (engineController.NetworkHandler is null) { return; }

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
        INetworkUserContext context = BuildConnectionContext(userName);
        Run(handler => handler.OnConnected(context), "OnConnected", userName);
        return Task.CompletedTask;
    }

    private Task OnUserDisconnected(string userName)
    {
        INetworkUserContext context = BuildConnectionContext(userName);
        Run(handler => handler.OnDisconnected(context), "OnDisconnected", userName);
        return Task.CompletedTask;
    }

    private Task OnMessageDelivered(object payload)
    {
        INetworkMessageContext context = BuildMessageContext(payload);
        Run(handler => handler.OnReceived(context), "OnReceived", engineController.GetMessageId(payload));
        return Task.CompletedTask;
    }

    private void Run(Func<INetworkHandler, Task> run, string name, string subject)
    {
        if (engineController.NetworkHandler is not { } handler) { return; }

        _ = Observe(run, handler, name, subject);
    }

    private async Task Observe(Func<INetworkHandler, Task> run, INetworkHandler handler, string name, string subject)
    {
        try { await run(handler); }
        catch (Exception ex) { logger.LogError(ex, "The network processor's {Name} failed for {Subject}", name, subject); }
    }

    private INetworkUserContext BuildConnectionContext(string targetUser)
        => new NetworkUserContext(BuildEngineContext(), targetUser, engineController, messageRouting, logger);

    private INetworkMessageContext BuildMessageContext(object message)
        => new NetworkMessageContext(BuildEngineContext(), message, engineController, messageRouting, logger);

    private EngineContext BuildEngineContext()
        => new(
            userService.GetCurrentUserInfo()
                ?? throw new InvalidOperationException("A processor ran with no installed user, which should never happen: EngineHooksService.Start only ever runs once one is installed."),
            engineController.Users,
            engineController.UserGroups,
            peerService.IsUserConnected);
}
