namespace BlueHeighliner.Comlink.Engine.Services;

/// <summary>Routes outbound messages to peer users and surfaces their OFT delivery status.</summary>
internal interface IMessageRoutingService
{
    /// <summary>Raised whenever the delivery status for a specific user transitions.</summary>
    event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;
    /// <summary>
    /// Expands groups in <paramref name="payload"/> addresses, sends to all resolved users, and returns per-user delivery results.
    /// Each result includes the top-level addressed group names through which the user was reached. A remote user's
    /// <see cref="UserDeliveryResult.Success"/> already reflects full OFT delivery — the underlying send only completes
    /// once OFT has fully acknowledged the message — and the sending user addressing itself is always delivered
    /// in-process, so this method never returns before every recipient's outcome, remote or local, is final.
    /// </summary>
    Task<(string MessageId, IReadOnlyList<UserDeliveryResult> UserResults)> Route(string fromUser, SendMessagePayload payload, CancellationToken cancellation);
}

/// <summary>Routes outbound messages to peer users and surfaces their OFT delivery status.</summary>
internal sealed class MessageRoutingService : IMessageRoutingService
{
    private static DestinationStatus MapStatus(OftDeliveryStatus status) => status switch
    {
        OftDeliveryStatus.Sent => DestinationStatus.Sent,
        OftDeliveryStatus.Acknowledged => DestinationStatus.Confirmed,
        OftDeliveryStatus.Cancelled => DestinationStatus.Failed,
        _ => DestinationStatus.Sending
    };

    private static void ExpandGroup(string groupName, IReadOnlyDictionary<string, IReadOnlyList<string>> groupMap, HashSet<string> users, HashSet<string> visited)
    {
        if (!visited.Add(groupName)) { return; }
        if (!groupMap.TryGetValue(groupName, out IReadOnlyList<string>? members)) { return; }
        foreach (string member in members)
        {
            if (groupMap.ContainsKey(member))
            {
                ExpandGroup(member, groupMap, users, visited);
            }
            else
            {
                users.Add(member);
            }
        }
    }

    /// <summary>Initializes a new <see cref="MessageRoutingService"/> and subscribes to peer delivery status events.</summary>
    /// <param name="peerService">Peer service for sending and receiving messages.</param>
    /// <param name="engineController">Provides group definitions for address expansion and maps logical fields onto the engine's message type when building outbound messages.</param>
    /// <param name="loggerFactory">Factory for creating named loggers.</param>
    public MessageRoutingService(IPeerService peerService, IEngineController engineController, ILoggerFactory loggerFactory)
    {
        this.peerService = peerService;
        this.engineController = engineController;
        logger = loggerFactory.CreateLogger("ACTIVITY");

        peerService.DeliveryStatusChanged += OnPeerDeliveryStatusChanged;
        peerService.ConfirmationReceived += OnPeerConfirmationReceived;
    }

    private readonly IPeerService peerService;
    private readonly IEngineController engineController;
    private readonly ILogger logger;

    /// <inheritdoc />
    public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;

    private async Task OnPeerDeliveryStatusChanged(string messageId, string user, OftDeliveryStatus status)
    {
        DestinationStatus mapped = MapStatus(status);
        logger.LogInformation("{MessageId} status for {User}: {Status}", messageId, user, mapped);
        if (DeliveryStatusChanged is not null)
        {
            await DeliveryStatusChanged(messageId, user, mapped);
        }
    }

    private async Task OnPeerConfirmationReceived(string messageId, string confirmingUser)
    {
        if (DeliveryStatusChanged is not null)
        {
            await DeliveryStatusChanged(messageId, confirmingUser, DestinationStatus.Read);
        }
    }

    /// <inheritdoc />
    public async Task<(string MessageId, IReadOnlyList<UserDeliveryResult> UserResults)> Route(string fromUser, SendMessagePayload payload, CancellationToken cancellation)
    {
        string messageId = Guid.NewGuid().ToString("N").ToUpperInvariant();
        DateTime sentAt = DateTime.UtcNow;

        IReadOnlyDictionary<string, IReadOnlyList<string>> groupMap = engineController.UserGroups;

        // Expand group addresses to individual users, tracking which top-level addressed groups contain each user.
        Dictionary<string, List<string>> userAddressedVia = new(StringComparer.OrdinalIgnoreCase);
        foreach (AddressPayload address in payload.Addresses)
        {
            if (groupMap.ContainsKey(address.UserName))
            {
                HashSet<string> expanded = new(StringComparer.OrdinalIgnoreCase);
                ExpandGroup(address.UserName, groupMap, expanded, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                foreach (string user in expanded)
                {
                    if (!userAddressedVia.TryGetValue(user, out List<string>? via))
                    {
                        via = [];
                        userAddressedVia[user] = via;
                    }
                    if (!via.Contains(address.UserName, StringComparer.OrdinalIgnoreCase))
                    {
                        via.Add(address.UserName);
                    }
                }
            }
            else if (!userAddressedVia.ContainsKey(address.UserName))
            {
                userAddressedVia[address.UserName] = [];
            }
        }

        List<string> targetUsers = [.. userAddressedVia.Keys];
        logger.LogInformation("{MessageId} sending to {Destinations}", messageId, string.Join(", ", targetUsers));

        object message = engineController.CreateMessage();
        engineController.SetMessageId(message, messageId);
        engineController.SetFromUser(message, fromUser);
        engineController.SetSubject(message, payload.Subject);
        engineController.SetBody(message, payload.Body);
        engineController.SetAddresses(message, payload.Addresses.Select(a => new MessageAddress { UserName = a.UserName, Type = a.Type.ParseAddressType() }).ToList());
        engineController.SetSentAt(message, sentAt);
        engineController.SetIsAlert(message, payload.IsAlert);
        engineController.SetPriority(message, payload.Priority);
        engineController.SetTag(message, payload.Tag);

        string? selfUser = targetUsers.FirstOrDefault(user => string.Equals(user, fromUser, StringComparison.OrdinalIgnoreCase));
        List<string> remoteUsers = selfUser is null ? targetUsers : targetUsers.Where(user => !string.Equals(user, fromUser, StringComparison.OrdinalIgnoreCase)).ToList();

        UserDeliveryResult[] remoteResults = await Task.WhenAll(remoteUsers.Select(async user =>
        {
            bool sent = await peerService.Send(user, message, cancellation);
            IReadOnlyList<string> via = userAddressedVia.TryGetValue(user, out List<string>? v) ? v.AsReadOnly() : Array.Empty<string>();
            logger.LogInformation(sent ? "{MessageId} delivered to {User}" : "{MessageId} failed to {User}", messageId, user);
            return new UserDeliveryResult { UserName = user, Success = sent, AddressedVia = [.. via] };
        }));

        List<UserDeliveryResult> allResults = [.. remoteResults];

        if (selfUser is not null)
        {
            await peerService.DeliverLocal(message);
            IReadOnlyList<string> via = userAddressedVia.TryGetValue(selfUser, out List<string>? v) ? v.AsReadOnly() : Array.Empty<string>();
            logger.LogInformation("{MessageId} delivered locally to {User}", messageId, selfUser);
            if (DeliveryStatusChanged is not null)
            {
                await DeliveryStatusChanged(messageId, selfUser, DestinationStatus.Confirmed);
            }
            allResults.Add(new UserDeliveryResult { UserName = selfUser, Success = true, AddressedVia = [.. via] });
        }

        return (messageId, allResults);
    }
}
