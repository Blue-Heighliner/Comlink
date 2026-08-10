namespace BlueHeighliner.Comlink.Engine.Services;

/// <summary>Routes outbound messages to peer sites and surfaces their OFT delivery status.</summary>
internal interface IMessageRoutingService
{
    /// <summary>Raised whenever the delivery status for a specific site transitions.</summary>
    event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;
    /// <summary>
    /// Expands groups in <paramref name="payload"/> addresses, sends to all resolved sites, and returns per-site delivery results.
    /// Each result includes the top-level addressed group names through which the site was reached. A remote site's
    /// <see cref="SiteDeliveryResult.Success"/> already reflects full OFT delivery — the underlying send only completes
    /// once OFT has fully acknowledged the message — and the sending site addressing itself is always delivered
    /// in-process, so this method never returns before every recipient's outcome, remote or local, is final.
    /// </summary>
    Task<(string MessageId, IReadOnlyList<SiteDeliveryResult> SiteResults)> Route(string fromSite, SendMessagePayload payload, CancellationToken cancellation);
}

/// <summary>Routes outbound messages to peer sites and surfaces their OFT delivery status.</summary>
internal sealed class MessageRoutingService : IMessageRoutingService
{
    private readonly IPeerService _peerService;
    private readonly ISiteGroupProvider _groupProvider;
    private readonly IMessageFormat _messageFormat;
    private readonly ILogger _logger;

    /// <inheritdoc />
    public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;

    /// <summary>Initializes a new <see cref="MessageRoutingService"/> and subscribes to peer delivery status events.</summary>
    /// <param name="peerService">Peer service for sending and receiving messages.</param>
    /// <param name="groupProvider">Provides group definitions for address expansion.</param>
    /// <param name="messageFormat">Maps logical fields onto the engine's message type when building outbound messages.</param>
    /// <param name="loggerFactory">Factory for creating named loggers.</param>
    public MessageRoutingService(IPeerService peerService, ISiteGroupProvider groupProvider, IMessageFormat messageFormat, ILoggerFactory loggerFactory)
    {
        _peerService = peerService;
        _groupProvider = groupProvider;
        _messageFormat = messageFormat;
        _logger = loggerFactory.CreateLogger("ACTIVITY");

        peerService.DeliveryStatusChanged += OnPeerDeliveryStatusChanged;
    }

    private async Task OnPeerDeliveryStatusChanged(string messageId, string site, OftDeliveryStatus status)
    {
        DestinationStatus mapped = MapStatus(status);
        _logger.LogInformation("{MessageId} status for {Site}: {Status}", messageId, site, mapped);
        if (DeliveryStatusChanged is not null)
            await DeliveryStatusChanged(messageId, site, mapped);
    }

    private static DestinationStatus MapStatus(OftDeliveryStatus status) => status switch
    {
        OftDeliveryStatus.Sent => DestinationStatus.Sent,
        OftDeliveryStatus.Acknowledged => DestinationStatus.Confirmed,
        OftDeliveryStatus.Cancelled => DestinationStatus.Failed,
        _ => DestinationStatus.Sending
    };

    /// <inheritdoc />
    public async Task<(string MessageId, IReadOnlyList<SiteDeliveryResult> SiteResults)> Route(string fromSite, SendMessagePayload payload, CancellationToken cancellation)
    {
        string messageId = Guid.NewGuid().ToString("N").ToUpperInvariant();
        DateTime sentAt = DateTime.UtcNow;

        IReadOnlyDictionary<string, IReadOnlyList<string>> groupMap = await _groupProvider.GetGroups(cancellation);

        // Expand group addresses to individual sites, tracking which top-level addressed groups contain each site.
        Dictionary<string, List<string>> siteAddressedVia = new(StringComparer.OrdinalIgnoreCase);
        foreach (AddressPayload address in payload.Addresses)
        {
            if (groupMap.ContainsKey(address.SiteName))
            {
                HashSet<string> expanded = new(StringComparer.OrdinalIgnoreCase);
                ExpandGroup(address.SiteName, groupMap, expanded, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                foreach (string site in expanded)
                {
                    if (!siteAddressedVia.TryGetValue(site, out List<string>? via))
                    {
                        via = [];
                        siteAddressedVia[site] = via;
                    }
                    if (!via.Contains(address.SiteName, StringComparer.OrdinalIgnoreCase))
                        via.Add(address.SiteName);
                }
            }
            else if (!siteAddressedVia.ContainsKey(address.SiteName))
            {
                siteAddressedVia[address.SiteName] = [];
            }
        }

        List<string> targetSites = [.. siteAddressedVia.Keys];
        _logger.LogInformation("{MessageId} sending to {Destinations}", messageId, string.Join(", ", targetSites));

        object message = _messageFormat.CreateMessage();
        _messageFormat.SetMessageId(message, messageId);
        _messageFormat.SetFromSite(message, fromSite);
        _messageFormat.SetSubject(message, payload.Subject);
        _messageFormat.SetBody(message, payload.Body);
        _messageFormat.SetAddresses(message, payload.Addresses.Select(a => new MessageAddress { SiteName = a.SiteName, Type = a.Type.ParseAddressType() }).ToList());
        _messageFormat.SetSentAt(message, sentAt);

        string? selfSite = targetSites.FirstOrDefault(site => string.Equals(site, fromSite, StringComparison.OrdinalIgnoreCase));
        List<string> remoteSites = selfSite is null ? targetSites : targetSites.Where(site => !string.Equals(site, fromSite, StringComparison.OrdinalIgnoreCase)).ToList();

        SiteDeliveryResult[] remoteResults = await Task.WhenAll(remoteSites.Select(async site =>
        {
            bool sent = await _peerService.Send(site, message, cancellation);
            IReadOnlyList<string> via = siteAddressedVia.TryGetValue(site, out List<string>? v) ? v.AsReadOnly() : Array.Empty<string>();
            _logger.LogInformation(sent ? "{MessageId} delivered to {Site}" : "{MessageId} failed to {Site}", messageId, site);
            return new SiteDeliveryResult { SiteName = site, Success = sent, AddressedVia = [.. via] };
        }));

        List<SiteDeliveryResult> allResults = [.. remoteResults];

        if (selfSite is not null)
        {
            await _peerService.DeliverLocal(message);
            IReadOnlyList<string> via = siteAddressedVia.TryGetValue(selfSite, out List<string>? v) ? v.AsReadOnly() : Array.Empty<string>();
            _logger.LogInformation("{MessageId} delivered locally to {Site}", messageId, selfSite);
            if (DeliveryStatusChanged is not null)
                await DeliveryStatusChanged(messageId, selfSite, DestinationStatus.Confirmed);
            allResults.Add(new SiteDeliveryResult { SiteName = selfSite, Success = true, AddressedVia = [.. via] });
        }

        return (messageId, allResults);
    }

    private static void ExpandGroup(string groupName, IReadOnlyDictionary<string, IReadOnlyList<string>> groupMap, HashSet<string> sites, HashSet<string> visited)
    {
        if (!visited.Add(groupName)) return;
        if (!groupMap.TryGetValue(groupName, out IReadOnlyList<string>? members)) return;
        foreach (string member in members)
        {
            if (groupMap.ContainsKey(member))
                ExpandGroup(member, groupMap, sites, visited);
            else
                sites.Add(member);
        }
    }
}
