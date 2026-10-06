namespace BlueHeighliner.Comlink;

/// <summary>In-process <see cref="IServiceConnection"/> implementation that wires directly to engine services without a network hop.</summary>
internal sealed class DirectServiceConnection : IServiceConnection
{
    /// <summary>Initializes a new <see cref="DirectServiceConnection"/> with the required engine services.</summary>
    public DirectServiceConnection(
        IUserService userService,
        IEngineController engineController,
        IMessageRoutingService messageRouting,
        IPeerService peerService,
        IEntryService entryService)
    {
        this.userService = userService;
        this.engineController = engineController;
        this.messageRouting = messageRouting;
        this.peerService = peerService;
        this.entryService = entryService;
    }

    private readonly IUserService userService;
    private readonly IEngineController engineController;
    private readonly IMessageRoutingService messageRouting;
    private readonly IPeerService peerService;
    private readonly IEntryService entryService;

    /// <inheritdoc />
    public event Func<MessageReceivedEvent, Task>? MessageReceived;

    /// <inheritdoc />
    public event Func<DeliveryStatusChangedEvent, Task>? DeliveryStatusChanged;

    /// <inheritdoc />
    public Task Connect(CancellationToken cancellation = default)
    {
        peerService.FrameDelivered += OnMessageDelivered;
        messageRouting.DeliveryStatusChanged += OnDeliveryStatusChanged;
        return Task.CompletedTask;
    }

    private async Task OnMessageDelivered(object payload)
    {
        if (!engineController.IsMessage(payload)) { return; }

        await SendReceiveReceipt(payload);
        if (!engineController.AcceptAlert(payload)) { return; }

        if (MessageReceived is null) { return; }
        await MessageReceived.InvokeAll(engineController.ToMessageReceivedEvent(payload));
    }

    private async Task SendReceiveReceipt(object message)
    {
        UserInfo? userInfo = userService.GetCurrentUserInfo();
        string fromUser = engineController.GetFromUser(message);
        if (userInfo is null) { return; }

        object receipt = engineController.CreateReceiveReceipt(engineController.GetMessageId(message), fromUser);
        engineController.SetFromUser(receipt, userInfo.Name);
        await peerService.Send(fromUser, receipt);
    }

    private async Task OnDeliveryStatusChanged(string messageId, string user, DestinationStatus status)
    {
        MessageEntity? entity = await entryService.UpdateDeliveryStatus(messageId, user, status);
        if (entity is null || DeliveryStatusChanged is null) { return; }

        // Reports the status as stored rather than as raised, since a late, out-of-order event may have been ignored.
        DestinationStatus effective = entity.DeliveryStatuses
            .FirstOrDefault(d => string.Equals(d.UserName, user, StringComparison.OrdinalIgnoreCase))?.Status ?? status;
        await DeliveryStatusChanged.InvokeAll(new DeliveryStatusChangedEvent { MessageId = messageId, UserName = user, Status = effective, OverallStatus = entity.OverallStatus });
    }

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
    public Task<UserInfo?> InstallUser(string userCode, CancellationToken cancellation = default)
        => userService.Install(userCode, cancellation);

    /// <inheritdoc />
    public async Task<SendMessageResult?> SendMessage(string body, List<AddressRequest> addresses, Enum? priority = null, string tag = "", Enum? securityLevel = null, CancellationToken cancellation = default)
    {
        UserInfo? userInfo = userService.GetCurrentUserInfo();
        if (userInfo is null) { return null; }

        if (engineController.TagsEnabled && engineController.DraftTagRules.Validate(tag) is { } tagError) { throw new ArgumentException(tagError, nameof(tag)); }

        SendMessagePayload payload = new()
        {
            Body = body,
            Addresses = addresses.Select(a => new AddressPayload { UserName = a.UserName, Type = a.Type, Information = a.Information }).ToList(),
            Priority = priority,
            Tag = tag,
            SecurityLevel = engineController.GetSecurityLevelName(securityLevel)
        };

        (string messageId, IReadOnlyList<UserDeliveryResult> userResults) = await messageRouting.Route(userInfo.Name, payload, cancellation);
        return new SendMessageResult
        {
            MessageId = messageId,
            IsAlert = engineController.ComputeIsAlert(body, priority, tag, engineController.GetSecurityLevelName(securityLevel), addresses),
            UserResults = [.. userResults]
        };
    }

    /// <inheritdoc />
    public async Task<bool> MarkMessageRead(string messageId, CancellationToken cancellation = default)
    {
        MessageEntity? entity = await entryService.MarkMessageRead(messageId);
        if (entity is null) { return false; }

        await DeliveryStatusChanged.InvokeAll(new DeliveryStatusChangedEvent { MessageId = messageId, Status = DestinationStatus.Read, OverallStatus = DestinationStatus.Read });

        UserInfo? userInfo = userService.GetCurrentUserInfo();
        if (userInfo is null) { return true; }

        string fromUser = engineController.GetFromUser(entity.Message);
        object receipt = engineController.CreateReadReceipt(messageId, fromUser);
        engineController.SetFromUser(receipt, userInfo.Name);
        await peerService.Send(fromUser, receipt, cancellation);
        return true;
    }
}
