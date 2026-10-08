namespace BlueHeighliner.Comlink;

/// <summary>In-process <see cref="IEngineConnection"/> implementation that wires directly to engine services without a network hop.</summary>
internal sealed class DirectServiceConnection : IEngineConnection
{
    /// <summary>Initializes a new <see cref="DirectServiceConnection"/> with the required engine services.</summary>
    public DirectServiceConnection(
        IUserService userService,
        IEngineController engineController,
        IPeerService peerService,
        IEntryService entryService,
        IMessageEvents events,
        INetworkProcessing processing,
        IIdGenerator ids,
        ILoggerFactory loggerFactory)
    {
        logger = loggerFactory.CreateLogger(LogCategories.App);
        this.userService = userService;
        this.engineController = engineController;
        this.peerService = peerService;
        this.entryService = entryService;
        this.events = events;
        this.processing = processing;
        this.ids = ids;
    }

    private readonly IUserService userService;
    private readonly IEngineController engineController;
    private readonly IPeerService peerService;
    private readonly IEntryService entryService;
    private readonly IMessageEvents events;
    private readonly INetworkProcessing processing;
    private readonly IIdGenerator ids;
    private readonly ILogger logger;

    /// <inheritdoc />
    public event Func<Message, Task>? MessageReceived;

    /// <inheritdoc />
    public event Func<DeliveryStatusChangedEvent, Task>? DeliveryStatusChanged;

    /// <inheritdoc />
    public Task Connect(CancellationToken cancellation = default)
    {
        events.MessageReceived += OnMessageReceived;
        events.DeliveryStatusChanged += OnDeliveryStatusChanged;
        return Task.CompletedTask;
    }

    private Task OnMessageReceived(Message message) => MessageReceived.InvokeAll(message);

    private Task OnDeliveryStatusChanged(DeliveryStatusChangedEvent change) => DeliveryStatusChanged.InvokeAll(change);

    /// <inheritdoc />
    public Task<UserInfo?> GetUserInfo(CancellationToken cancellation = default)
        => Task.FromResult(userService.GetCurrentUserInfo());

    /// <inheritdoc />
    public Task<List<string>> GetUserNames(CancellationToken cancellation = default)
    {
        try
        {
            return Task.FromResult<List<string>>([.. engineController.Users]);
        }
        catch
        {
            return Task.FromResult<List<string>>([]);
        }
    }

    /// <inheritdoc />
    public Task<List<string>> GetConnectedUsers(CancellationToken cancellation = default)
        => Task.FromResult<List<string>>([.. peerService.GetConnectedUsers()]);

    /// <inheritdoc />
    public Task<UserInfo?> InstallUser(string userName, CancellationToken cancellation = default)
        => userService.Install(userName, cancellation);

    /// <inheritdoc />
    public async Task<SendMessageResult?> SendMessage(string body, List<AddressRequest> addresses, Enum? priority = null, string tag = "", Enum? messageLevel = null, Enum? messageAspect = null, CancellationToken cancellation = default)
    {
        UserInfo? userInfo = userService.GetCurrentUserInfo();
        if (userInfo is null)
        {
            return null;
        }

        if (engineController.TagsEnabled && engineController.DraftTagRules.Validate(tag) is { } tagError)
        {
            throw new ArgumentException(tagError, nameof(tag));
        }

        Enum resolvedPriority = engineController.RequirePriority(priority);
        string levelName = engineController.GetMessageLevelName(messageLevel);
        string aspectName = engineController.GetMessageAspectName(messageAspect);
        bool isAlert = engineController.IsAlert(new DraftContent { Tag = tag, Priority = resolvedPriority, MessageLevel = levelName, MessageAspect = aspectName, Body = body, Addresses = addresses, LineWidth = null });

        Message message = new()
        {
            Id = await ids.Next(),
            FromUser = userInfo.Name,
            Body = body,
            Addresses = [.. addresses.Select(a => new MessageAddress { UserName = a.UserName, Type = a.Type.ParseAddressType(), Information = a.Information })],
            SentAt = DateTime.UtcNow,
            Priority = resolvedPriority,
            Tag = tag,
            MessageLevel = messageLevel,
            MessageAspect = messageAspect,
            IsAlert = isAlert
        };

        await entryService.StoreSentMessage(message);
        logger.Record(LogEvents.MessageSending, "{MessageId} sending to {Destinations}", message.Id, string.Join(", ", message.Addresses.Where(a => a.Type is not AddressType.External).Select(a => a.UserName).Distinct(StringComparer.OrdinalIgnoreCase)));
        processing.Sent(message);
        return new SendMessageResult { MessageId = message.Id, IsAlert = isAlert };
    }

    /// <inheritdoc />
    public async Task<bool> MarkMessageRead(string messageId, CancellationToken cancellation = default)
    {
        MessageEntity? entity = await entryService.MarkMessageRead(messageId);
        if (entity is null)
        {
            return false;
        }

        await events.RaiseDeliveryStatusChanged(new DeliveryStatusChangedEvent { MessageId = messageId, Status = DestinationStatus.Read, OverallStatus = DestinationStatus.Read });
        processing.Read(engineController.ToMessage(entity.Message));
        return true;
    }
}
