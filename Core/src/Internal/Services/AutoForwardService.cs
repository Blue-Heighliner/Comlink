namespace BlueHeighliner.Comlink;

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
        logger = loggerFactory.CreateLogger(LogCategories.App);
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
                string filteredId = engineController.GetMessageId(message);
                logger.Record(LogEvents.AutoForwardFailed, ex, "Auto forward controller {Controller} failed to {Action} {MessageId}", controller.Name, "filter", filteredId);
                logger.Record(LogEvents.AutoForwardNotDone, "Message {MessageId} could not be forwarded automatically", filteredId);
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
            object forwarded = engineController.CreateMessage(new MessageContent
            {
                SentAt = DateTime.UtcNow,
                Body = engineController.GetBody(original),
                Priority = engineController.GetMessagePriority(original),
                Tag = engineController.GetTag(original),
                MessageLevel = engineController.GetMessageLevel(original)
            });
            engineController.SetAddresses(forwarded, [.. recipients.Select(name => new MessageAddress { UserName = name, Type = AddressType.To })]);

            await messageRouting.RouteFrame(fromUser, forwarded, CancellationToken.None);
        }
        catch (Exception ex)
        {
            string forwardedId = engineController.GetMessageId(original);
            logger.Record(LogEvents.AutoForwardFailed, ex, "Auto forward controller {Controller} failed to {Action} {MessageId}", controllerName, "forward", forwardedId);
            logger.Record(LogEvents.AutoForwardNotDone, "Message {MessageId} could not be forwarded automatically", forwardedId);
        }
    }
}
