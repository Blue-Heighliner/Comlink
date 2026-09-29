namespace BlueHeighliner.Comlink.Services;

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
        peerService.MessageDelivered += OnMessageDelivered;
        messageRouting.DeliveryStatusChanged += OnDeliveryStatusChanged;
        return Task.CompletedTask;
    }

    private async Task OnMessageDelivered(object payload)
    {
        if (MessageReceived is null) { return; }
        MessageReceivedEvent evt = new()
        {
            MessageId = engineController.GetMessageId(payload),
            FromUser = engineController.GetFromUser(payload),
            Subject = engineController.GetSubject(payload),
            Body = engineController.GetBody(payload),
            Addresses = engineController.GetAddresses(payload).Select(a => new AddressRequest { UserName = a.UserName, Type = a.Type.ToString(), Information = a.Information }).ToList(),
            SentAt = engineController.GetSentAt(payload),
            IsAlert = engineController.GetIsAlert(payload),
            Priority = engineController.GetPriority(payload),
            Tag = engineController.GetTag(payload),
            SecurityLevel = engineController.GetSecurityLevel(payload)
        };
        await MessageReceived.InvokeAll(evt);
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
    public Task<UserInfo?> InstallUser(string userCode, CancellationToken cancellation = default)
        => userService.Install(userCode, cancellation);

    /// <inheritdoc />
    public async Task<SendMessageResult?> SendMessage(string subject, string body, List<AddressRequest> addresses, bool isAlert = false, int priority = 0, string tag = "", string securityLevel = "", CancellationToken cancellation = default)
    {
        UserInfo? userInfo = userService.GetCurrentUserInfo();
        if (userInfo is null) { return null; }

        SendMessagePayload payload = new()
        {
            Subject = subject,
            Body = body,
            Addresses = addresses.Select(a => new AddressPayload { UserName = a.UserName, Type = a.Type, Information = a.Information }).ToList(),
            IsAlert = isAlert,
            Priority = priority,
            Tag = tag,
            SecurityLevel = securityLevel
        };

        (string messageId, IReadOnlyList<UserDeliveryResult> userResults) = await messageRouting.Route(userInfo.Name, payload, cancellation);
        return new SendMessageResult
        {
            MessageId = messageId,
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
        if (string.Equals(fromUser, userInfo.Name, StringComparison.OrdinalIgnoreCase))
        {
            // Self-addressed message: no network hop needed, mirroring MessageRoutingService.Route's own self-delivery bypass.
            await entryService.UpdateDeliveryStatus(messageId, fromUser, DestinationStatus.Read);
            return true;
        }

        object confirmation = engineController.CreateMessage();
        engineController.SetMessageId(confirmation, Guid.NewGuid().ToString("N").ToUpperInvariant());
        engineController.SetFromUser(confirmation, userInfo.Name);
        engineController.SetConfirmationMessageId(confirmation, messageId);
        engineController.SetAddresses(confirmation, [new MessageAddress { UserName = fromUser, Type = AddressType.To }]);
        engineController.SetSentAt(confirmation, DateTime.UtcNow);
        await peerService.Send(fromUser, confirmation, cancellation);
        return true;
    }
}
