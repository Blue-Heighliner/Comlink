namespace BlueHeighliner.Comlink;

/// <summary>Routes outbound messages to peer users and surfaces their delivery status.</summary>
internal interface IMessageRoutingService
{
    /// <summary>Raised whenever the delivery status for a specific user transitions.</summary>
    event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;
    /// <summary>
    /// Expands groups in <paramref name="payload"/> addresses, sends to all resolved users, and returns per-user delivery results.
    /// Each result includes the top-level addressed group names through which the user was reached. A remote user's
    /// <see cref="UserDeliveryResult.Success"/> already reflects full delivery — the underlying send only completes
    /// once the peer transport has fully acknowledged the message — and the sending user addressing itself is always
    /// delivered in-process, so this method never returns before every recipient's outcome, remote or local, is final.
    /// </summary>
    Task<(string MessageId, IReadOnlyList<UserDeliveryResult> UserResults)> Route(string fromUser, SendMessagePayload payload, CancellationToken cancellation);

    /// <summary>
    /// The same as <see cref="Route"/>, except every field this reads (security level) comes straight from
    /// <paramref name="message"/> itself, via <see cref="IEngineController"/>'s Get accessors, and who it goes to from
    /// <see cref="IEngineController.Route"/>, rather than from a <see cref="SendMessagePayload"/> - so the caller builds the whole frame (an instance of
    /// <see cref="IEngineController.FrameType"/>) itself instead of stating loose fields. Its sender is overwritten with <paramref name="fromUser"/>,
    /// and a message whose identifier is unset is given a generated one, so a caller only needs to set the content fields,
    /// including the sent time when the frame is a message. Any other kind of frame has no identifier.
    /// </summary>
    Task<(string MessageId, IReadOnlyList<UserDeliveryResult> UserResults)> RouteFrame(string fromUser, object message, CancellationToken cancellation);
}

/// <summary>Routes outbound messages to peer users and surfaces their delivery status.</summary>
internal sealed class MessageRoutingService : IMessageRoutingService
{
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
    /// <param name="engineController">Provides group definitions for address expansion and maps logical fields onto the engine's frame type when building outbound messages.</param>
    /// <param name="ids">Generates the identifier of each message built.</param>
    /// <param name="loggerFactory">Factory for creating named loggers.</param>
    public MessageRoutingService(IPeerService peerService, IEngineController engineController, IIdGenerator ids, ILoggerFactory loggerFactory)
    {
        this.ids = ids;
        this.peerService = peerService;
        this.engineController = engineController;
        logger = loggerFactory.CreateLogger(LogCategories.App);

        peerService.DeliveryStatusChanged += OnPeerDeliveryStatusChanged;
        peerService.ReadReceiptReceived += OnPeerReadReceiptReceived;
        peerService.ReceiveReceiptReceived += OnPeerReceiveReceiptReceived;
    }

    private readonly IPeerService peerService;
    private readonly IEngineController engineController;
    private readonly IIdGenerator ids;
    private readonly ILogger logger;

    /// <inheritdoc />
    public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;

    private async Task OnPeerDeliveryStatusChanged(string messageId, string user, DestinationStatus status)
    {
        logger.Record(LogEvents.DeliveryStatusChanged, "{MessageId} status for {User}: {Status}", messageId, user, status);
        await DeliveryStatusChanged.InvokeAll(messageId, user, status);
    }

    private async Task OnPeerReadReceiptReceived(string messageId, string readingUser)
        => await DeliveryStatusChanged.InvokeAll(messageId, readingUser, DestinationStatus.Read);

    private async Task OnPeerReceiveReceiptReceived(string messageId, string receivingUser)
        => await DeliveryStatusChanged.InvokeAll(messageId, receivingUser, DestinationStatus.Received);

    /// <inheritdoc />
    public async Task<(string MessageId, IReadOnlyList<UserDeliveryResult> UserResults)> Route(string fromUser, SendMessagePayload payload, CancellationToken cancellation)
    {
        List<MessageAddress> addresses = [.. payload.Addresses.Select(a => new MessageAddress { UserName = a.UserName, Type = a.Type.ParseAddressType(), Information = a.Information })];

        object message = engineController.CreateMessage(new MessageContent
        {
            SentAt = DateTime.UtcNow,
            Body = payload.Body,
            Priority = engineController.RequirePriority(payload.Priority),
            Tag = payload.Tag,
            SecurityLevel = payload.SecurityLevel
        });
        engineController.SetFromUser(message, fromUser);
        engineController.SetAddresses(message, addresses);

        return await RouteBuiltMessage(fromUser, await EnsureId(message), message, payload.SecurityLevel, cancellation);
    }

    /// <inheritdoc />
    public async Task<(string MessageId, IReadOnlyList<UserDeliveryResult> UserResults)> RouteFrame(string fromUser, object message, CancellationToken cancellation)
    {
        engineController.SetFromUser(message, fromUser);

        return await RouteBuiltMessage(fromUser, await EnsureId(message), message, engineController.GetSecurityLevel(message), cancellation);
    }

    private async Task<string> EnsureId(object message)
    {
        if (!engineController.IsMessage(message)) { return string.Empty; }

        if (string.IsNullOrEmpty(engineController.GetMessageId(message))) { engineController.SetMessageId(message, await ids.Next()); }

        return engineController.GetMessageId(message);
    }

    private async Task<(string MessageId, IReadOnlyList<UserDeliveryResult> UserResults)> RouteBuiltMessage(
        string fromUser, string messageId, object message, string securityLevel, CancellationToken cancellation)
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> groupMap = engineController.UserGroups;

        // Expand group addresses to individual users, tracking which top-level addressed groups contain each user.
        Dictionary<string, List<string>> userAddressedVia = new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in engineController.Route(message))
        {
            if (groupMap.ContainsKey(name))
            {
                HashSet<string> expanded = new(StringComparer.OrdinalIgnoreCase);
                ExpandGroup(name, groupMap, expanded, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                foreach (string user in expanded)
                {
                    if (!userAddressedVia.TryGetValue(user, out List<string>? via))
                    {
                        via = [];
                        userAddressedVia[user] = via;
                    }
                    if (!via.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        via.Add(name);
                    }
                }
            }
            else if (!userAddressedVia.ContainsKey(name))
            {
                userAddressedVia[name] = [];
            }
        }

        List<string> targetUsers = [.. userAddressedVia.Keys];

        IReadOnlyList<SecurityLevel> securityLevels = engineController.SecurityLevels;
        int messageLevelRank = securityLevels.GetRank(securityLevel);
        List<string> blockedUsers = messageLevelRank < 0
            ? []
            : [.. targetUsers.Where(user => securityLevels.GetRank(engineController.GetUserSecurityLevel(user)) < messageLevelRank)];
        if (blockedUsers.Count > 0)
        {
            targetUsers = [.. targetUsers.Except(blockedUsers, StringComparer.OrdinalIgnoreCase)];
            logger.Record(
                LogEvents.BlockedBySecurityLevel, "{Subject} blocked for {Users}: {Reason}",
                messageId, string.Join(", ", blockedUsers), $"security level {securityLevel} not supported by destination");
        }

        logger.Record(LogEvents.MessageSending, "{MessageId} sending to {Destinations}", messageId, string.Join(", ", targetUsers));

        UserDeliveryResult[] remoteResults = await Task.WhenAll(targetUsers.Select(async user =>
        {
            bool sent = await peerService.Send(user, message, cancellation);
            IReadOnlyList<string> via = userAddressedVia.TryGetValue(user, out List<string>? v) ? v.AsReadOnly() : Array.Empty<string>();
            logger.Record(sent ? LogEvents.MessageDelivered : LogEvents.MessageDeliveryFailed, sent ? "{MessageId} delivered to {User}" : "{MessageId} failed to {User}", messageId, user);
            return new UserDeliveryResult { UserName = user, Success = sent, AddressedVia = [.. via] };
        }));

        List<UserDeliveryResult> allResults = [.. remoteResults];

        allResults.AddRange(blockedUsers.Select(user => new UserDeliveryResult
        {
            UserName = user,
            Success = false,
            AddressedVia = [.. userAddressedVia.TryGetValue(user, out List<string>? via) ? via : []]
        }));

        return (messageId, allResults);
    }
}
