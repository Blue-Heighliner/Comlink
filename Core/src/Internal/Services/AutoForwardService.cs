namespace BlueHeighliner.Comlink.Services;

/// <summary>
/// Runs the host's auto forward controllers (<see cref="IEngineController.AutoForwardControllers"/>) whenever
/// <see cref="IPeerService"/> delivers a new message, in both Client and Headless mode: for every controller this
/// instance's own installed user has access to, a matching message is forwarded, unchanged in body, to
/// every user on that controller's locally-saved target list.
/// </summary>
internal interface IAutoForwardService
{
    /// <summary>Subscribes to <see cref="IPeerService.FrameDelivered"/> and blocks until <paramref name="cancellation"/> is cancelled.</summary>
    Task Start(CancellationToken cancellation);
}

/// <inheritdoc cref="IAutoForwardService" />
internal sealed class AutoForwardService : IAutoForwardService
{
    /// <summary>Initializes a new <see cref="AutoForwardService"/>.</summary>
    public AutoForwardService(
        IPeerService peerService,
        IEngineController engineController,
        IUserService userService,
        IMessageRoutingService messageRouting,
        IAutoForwardTargetsRepository targetsRepository,
        ILoggerFactory loggerFactory)
    {
        this.peerService = peerService;
        this.engineController = engineController;
        this.userService = userService;
        this.messageRouting = messageRouting;
        this.targetsRepository = targetsRepository;
        logger = loggerFactory.CreateLogger("ACTIVITY");
    }

    private readonly IPeerService peerService;
    private readonly IEngineController engineController;
    private readonly IUserService userService;
    private readonly IMessageRoutingService messageRouting;
    private readonly IAutoForwardTargetsRepository targetsRepository;
    private readonly ILogger logger;

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        if (engineController.AutoForwardControllers.Count == 0) { return; }

        peerService.FrameDelivered += OnMessageDelivered;
        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
        finally { peerService.FrameDelivered -= OnMessageDelivered; }
    }

    private async Task OnMessageDelivered(object message)
    {
        if (!engineController.IsMessage(message)) { return; }

        UserInfo? currentUser = userService.GetCurrentUserInfo();
        if (currentUser is null) { return; }

        foreach (AutoForwardControllerDefinition controller in engineController.AutoForwardControllers)
        {
            if (!controller.Users.Contains(currentUser.Name, StringComparer.OrdinalIgnoreCase)) { continue; }

            bool matches;
            try { matches = controller.Filter(message); }
            catch (Exception ex)
            {
                logger.LogError(ex, "Auto forward controller {Controller} filter failed for {MessageId}", controller.Name, engineController.GetFrameId(message));
                continue;
            }
            if (!matches) { continue; }

            AutoForwardTargetsEntity? targets = await targetsRepository.Get(controller.Name);
            List<string> recipients = [.. (targets?.Targets ?? []).Where(t => !string.Equals(t, currentUser.Name, StringComparison.OrdinalIgnoreCase))];
            if (recipients.Count == 0) { continue; }

            await Forward(currentUser.Name, message, recipients, controller.Name);
        }
    }

    private async Task Forward(string fromUser, object original, List<string> recipients, string controllerName)
    {
        try
        {
            object forwarded = engineController.CreateMessage(new MessageCreateContext
            {
                SentAt = DateTime.UtcNow,
                Body = engineController.GetBody(original),
                IsAlert = engineController.GetIsAlert(original),
                Priority = engineController.GetPriority(original),
                Tag = engineController.GetTag(original),
                SecurityLevel = engineController.GetSecurityLevel(original)
            });
            engineController.SetAddresses(forwarded, [.. recipients.Select(name => new MessageAddress { UserName = name, Type = AddressType.To })]);

            await messageRouting.RouteFrame(fromUser, forwarded, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Auto forward controller {Controller} failed to forward {MessageId}", controllerName, engineController.GetFrameId(original));
        }
    }
}
