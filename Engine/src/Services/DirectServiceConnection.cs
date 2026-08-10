namespace BlueHeighliner.Comlink.Engine.Services;

/// <summary>In-process <see cref="IServiceConnection"/> implementation that wires directly to engine services without a network hop.</summary>
internal sealed class DirectServiceConnection : IServiceConnection
{
    private readonly ISiteService _siteService;
    private readonly ISiteCodeResolver _siteCodeResolver;
    private readonly ISiteNameDirectory _siteNameDirectory;
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
        ISiteService siteService,
        ISiteCodeResolver siteCodeResolver,
        ISiteNameDirectory siteNameDirectory,
        IMessageRoutingService messageRouting,
        IPeerService peerService,
        IEntryService entryService,
        IMessageFormat messageFormat)
    {
        _siteService = siteService;
        _siteCodeResolver = siteCodeResolver;
        _siteNameDirectory = siteNameDirectory;
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
            FromSite = _messageFormat.GetFromSite(payload),
            Subject = _messageFormat.GetSubject(payload),
            Body = _messageFormat.GetBody(payload),
            Addresses = _messageFormat.GetAddresses(payload).Select(a => new AddressRequest { SiteName = a.SiteName, Type = a.Type.ToString() }).ToList(),
            SentAt = _messageFormat.GetSentAt(payload)
        };
        await MessageReceived(evt);
    }

    private async Task OnDeliveryStatusChanged(string messageId, string site, DestinationStatus status)
    {
        MessageEntity? entity = await _entryService.UpdateDeliveryStatus(messageId, site, status);
        if (entity is not null && DeliveryStatusChanged is not null)
            await DeliveryStatusChanged(new DeliveryStatusChangedEvent { MessageId = messageId, SiteName = site, Status = status, OverallStatus = entity.OverallStatus });
    }

    /// <inheritdoc />
    public Task<SiteInfo?> GetSiteInfo(CancellationToken cancellation = default)
        => Task.FromResult(_siteService.GetCurrentSiteInfo());

    /// <inheritdoc />
    public async Task<List<string>> GetSiteNames(CancellationToken cancellation = default)
    {
        try
        {
            IReadOnlyList<string> names = await _siteNameDirectory.GetAllSiteNames(cancellation);
            return [.. names];
        }
        catch
        {
            return [];
        }
    }

    /// <inheritdoc />
    public Task<SiteInfo?> InstallSite(string siteCode, CancellationToken cancellation = default)
        => _siteService.Install(siteCode, cancellation);

    /// <inheritdoc />
    public async Task<SendMessageResult?> SendMessage(string subject, string body, List<AddressRequest> addresses, CancellationToken cancellation = default)
    {
        SiteInfo? siteInfo = _siteService.GetCurrentSiteInfo();
        if (siteInfo is null) return null;

        SendMessagePayload payload = new()
        {
            Subject = subject,
            Body = body,
            Addresses = addresses.Select(a => new AddressPayload { SiteName = a.SiteName, Type = a.Type }).ToList()
        };

        (string messageId, IReadOnlyList<SiteDeliveryResult> siteResults) = await _messageRouting.Route(siteInfo.Name, payload, cancellation);
        return new SendMessageResult
        {
            MessageId = messageId,
            SiteResults = [.. siteResults]
        };
    }
}
