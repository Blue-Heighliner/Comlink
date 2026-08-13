namespace BlueHeighliner.Comlink.Engine.Services;

/// <summary>In-process <see cref="IServiceConnection"/> implementation that wires directly to engine services without a network hop.</summary>
internal sealed class DirectServiceConnection : IServiceConnection
{
    private readonly IUserService _userService;
    private readonly IUserCodeResolver _userCodeResolver;
    private readonly IUserNameDirectory _userNameDirectory;
    private readonly IMessageRoutingService _messageRouting;
    private readonly IPeerService _peerService;
    private readonly IEntryService _entryService;
    private readonly IMessageFormat _messageFormat;

    /// <inheritdoc />
    public event Func<MessageReceivedEvent, Task>? MessageReceived;
    /// <inheritdoc />
    public event Func<DeliveryStatusChangedEvent, Task>? DeliveryStatusChanged;

    /// <summary>Initializes a new <see cref="DirectServiceConnection"/> with the required engine services.</summary>
    public DirectServiceConnection(
        IUserService userService,
        IUserCodeResolver userCodeResolver,
        IUserNameDirectory userNameDirectory,
        IMessageRoutingService messageRouting,
        IPeerService peerService,
        IEntryService entryService,
        IMessageFormat messageFormat)
    {
        _userService = userService;
        _userCodeResolver = userCodeResolver;
        _userNameDirectory = userNameDirectory;
        _messageRouting = messageRouting;
        _peerService = peerService;
        _entryService = entryService;
        _messageFormat = messageFormat;
    }

    /// <inheritdoc />
    public Task Connect(CancellationToken cancellation = default)
    {
        _peerService.MessageDelivered += OnMessageDelivered;
        _messageRouting.DeliveryStatusChanged += OnDeliveryStatusChanged;
        return Task.CompletedTask;
    }

    private async Task OnMessageDelivered(object payload)
    {
        if (MessageReceived is null) return;
        MessageReceivedEvent evt = new()
        {
            MessageId = _messageFormat.GetMessageId(payload),
            FromUser = _messageFormat.GetFromUser(payload),
            Subject = _messageFormat.GetSubject(payload),
            Body = _messageFormat.GetBody(payload),
            Addresses = _messageFormat.GetAddresses(payload).Select(a => new AddressRequest { UserName = a.UserName, Type = a.Type.ToString() }).ToList(),
            SentAt = _messageFormat.GetSentAt(payload),
            IsAlert = _messageFormat.GetIsAlert(payload),
            Priority = _messageFormat.GetPriority(payload)
        };
        await MessageReceived(evt);
    }

    private async Task OnDeliveryStatusChanged(string messageId, string user, DestinationStatus status)
    {
        MessageEntity? entity = await _entryService.UpdateDeliveryStatus(messageId, user, status);
        if (entity is not null && DeliveryStatusChanged is not null)
            await DeliveryStatusChanged(new DeliveryStatusChangedEvent { MessageId = messageId, UserName = user, Status = status, OverallStatus = entity.OverallStatus });
    }

    /// <inheritdoc />
    public Task<UserInfo?> GetUserInfo(CancellationToken cancellation = default)
        => Task.FromResult(_userService.GetCurrentUserInfo());

    /// <inheritdoc />
    public async Task<List<string>> GetUserNames(CancellationToken cancellation = default)
    {
        try
        {
            IReadOnlyList<string> names = await _userNameDirectory.GetAllUserNames(cancellation);
            return [.. names];
        }
        catch
        {
            return [];
        }
    }

    /// <inheritdoc />
    public Task<UserInfo?> InstallUser(string userCode, CancellationToken cancellation = default)
        => _userService.Install(userCode, cancellation);

    /// <inheritdoc />
    public async Task<SendMessageResult?> SendMessage(string subject, string body, List<AddressRequest> addresses, bool isAlert = false, int priority = 0, CancellationToken cancellation = default)
    {
        UserInfo? userInfo = _userService.GetCurrentUserInfo();
        if (userInfo is null) return null;

        SendMessagePayload payload = new()
        {
            Subject = subject,
            Body = body,
            Addresses = addresses.Select(a => new AddressPayload { UserName = a.UserName, Type = a.Type }).ToList(),
            IsAlert = isAlert,
            Priority = priority
        };

        (string messageId, IReadOnlyList<UserDeliveryResult> userResults) = await _messageRouting.Route(userInfo.Name, payload, cancellation);
        return new SendMessageResult
        {
            MessageId = messageId,
            UserResults = [.. userResults]
        };
    }

    /// <inheritdoc />
    public async Task<bool> MarkMessageRead(string messageId, CancellationToken cancellation = default)
    {
        MessageEntity? entity = await _entryService.MarkMessageRead(messageId);
        if (entity is null) return false;

        if (DeliveryStatusChanged is not null)
            await DeliveryStatusChanged(new DeliveryStatusChangedEvent { MessageId = messageId, Status = DestinationStatus.Read, OverallStatus = DestinationStatus.Read });

        UserInfo? userInfo = _userService.GetCurrentUserInfo();
        if (userInfo is null) return true;

        string fromUser = _messageFormat.GetFromUser(entity.Message);
        if (string.Equals(fromUser, userInfo.Name, StringComparison.OrdinalIgnoreCase))
        {
            // Self-addressed message: no network hop needed, mirroring MessageRoutingService.Route's own self-delivery bypass.
            await _entryService.UpdateDeliveryStatus(messageId, fromUser, DestinationStatus.Read);
            return true;
        }

        object confirmation = _messageFormat.CreateMessage();
        _messageFormat.SetMessageId(confirmation, Guid.NewGuid().ToString("N").ToUpperInvariant());
        _messageFormat.SetFromUser(confirmation, userInfo.Name);
        _messageFormat.SetConfirmationMessageId(confirmation, messageId);
        _messageFormat.SetSentAt(confirmation, DateTime.UtcNow);
        await _peerService.Send(fromUser, confirmation, cancellation);
        return true;
    }
}
