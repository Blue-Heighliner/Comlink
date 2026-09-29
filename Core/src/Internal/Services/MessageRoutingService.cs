namespace BlueHeighliner.Comlink.Services;

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
    /// The same as <see cref="Route"/>, except every field this reads (addresses, security level) comes straight from
    /// <paramref name="message"/> itself, via <see cref="Control.IEngineController"/>'s Get accessors, rather than
    /// from a <see cref="SendMessagePayload"/> - so the caller builds the whole message (an instance of
    /// <see cref="Control.IEngineController.MessageType"/>) itself instead of stating loose fields. Its message ID,
    /// sender, and sent time are still overwritten with a freshly generated ID, <paramref name="fromUser"/>, and the
    /// current UTC time, exactly as <see cref="Route"/> also does, so a caller only needs to set the content fields.
    /// </summary>
    Task<(string MessageId, IReadOnlyList<UserDeliveryResult> UserResults)> RouteMessage(string fromUser, object message, CancellationToken cancellation);
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

    private async Task OnPeerDeliveryStatusChanged(string messageId, string user, DestinationStatus status)
    {
        logger.LogInformation("{MessageId} status for {User}: {Status}", messageId, user, status);
        await DeliveryStatusChanged.InvokeAll(messageId, user, status);
    }

    private async Task OnPeerConfirmationReceived(string messageId, string confirmingUser)
    {
        await DeliveryStatusChanged.InvokeAll(messageId, confirmingUser, DestinationStatus.Read);
    }

    /// <inheritdoc />
    public Task<(string MessageId, IReadOnlyList<UserDeliveryResult> UserResults)> Route(string fromUser, SendMessagePayload payload, CancellationToken cancellation)
    {
        string messageId = Guid.NewGuid().ToString("N").ToUpperInvariant();
        List<MessageAddress> addresses = [.. payload.Addresses.Select(a => new MessageAddress { UserName = a.UserName, Type = a.Type.ParseAddressType(), Information = a.Information })];

        object message = engineController.CreateMessage();
        engineController.SetMessageId(message, messageId);
        engineController.SetFromUser(message, fromUser);
        engineController.SetSubject(message, payload.Subject);
        engineController.SetBody(message, payload.Body);
        engineController.SetAddresses(message, addresses);
        engineController.SetSentAt(message, DateTime.UtcNow);
        engineController.SetIsAlert(message, payload.IsAlert);
        engineController.SetPriority(message, payload.Priority);
        engineController.SetTag(message, payload.Tag);
        engineController.SetSecurityLevel(message, payload.SecurityLevel);

        return RouteBuiltMessage(fromUser, messageId, message, addresses, payload.SecurityLevel, cancellation);
    }

    /// <inheritdoc />
    public Task<(string MessageId, IReadOnlyList<UserDeliveryResult> UserResults)> RouteMessage(string fromUser, object message, CancellationToken cancellation)
    {
        string messageId = Guid.NewGuid().ToString("N").ToUpperInvariant();
        engineController.SetMessageId(message, messageId);
        engineController.SetFromUser(message, fromUser);
        engineController.SetSentAt(message, DateTime.UtcNow);

        return RouteBuiltMessage(fromUser, messageId, message, engineController.GetAddresses(message), engineController.GetSecurityLevel(message), cancellation);
    }

    private async Task<(string MessageId, IReadOnlyList<UserDeliveryResult> UserResults)> RouteBuiltMessage(
        string fromUser, string messageId, object message, List<MessageAddress> addresses, string securityLevel, CancellationToken cancellation)
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> groupMap = engineController.UserGroups;

        // Expand group addresses to individual users, tracking which top-level addressed groups contain each user.
        Dictionary<string, List<string>> userAddressedVia = new(StringComparer.OrdinalIgnoreCase);
        foreach (MessageAddress address in addresses)
        {
            if (address.Type == AddressType.External) { continue; }

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

        IReadOnlyList<SecurityLevel> securityLevels = engineController.SecurityLevels;
        int messageLevelRank = securityLevels.GetRank(securityLevel);
        List<string> blockedUsers = messageLevelRank < 0
            ? []
            : [.. targetUsers.Where(user => securityLevels.GetRank(engineController.GetUserSecurityLevel(user)) < messageLevelRank)];
        if (blockedUsers.Count > 0)
        {
            targetUsers = [.. targetUsers.Except(blockedUsers, StringComparer.OrdinalIgnoreCase)];
            logger.LogWarning(
                "{MessageId} blocked for {Users}: security level {Level} not supported by destination",
                messageId, string.Join(", ", blockedUsers), securityLevel);
        }

        logger.LogInformation("{MessageId} sending to {Destinations}", messageId, string.Join(", ", targetUsers));

        string? selfUser = targetUsers.FirstOrDefault(user => string.Equals(user, fromUser, StringComparison.OrdinalIgnoreCase));
        List<string> remoteUsers = selfUser is null ? targetUsers : targetUsers.Where(user => !string.Equals(user, fromUser, StringComparison.OrdinalIgnoreCase)).ToList();

        UserDeliveryResult[] remoteResults = engineController.ExternalServer is { } externalServer
            ? await RouteToExternalServer(externalServer, messageId, message, remoteUsers, userAddressedVia)
            : await Task.WhenAll(remoteUsers.Select(async user =>
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
            await DeliveryStatusChanged.InvokeAll(messageId, selfUser, DestinationStatus.Confirmed);
            allResults.Add(new UserDeliveryResult { UserName = selfUser, Success = true, AddressedVia = [.. via] });
        }

        allResults.AddRange(blockedUsers.Select(user => new UserDeliveryResult
        {
            UserName = user,
            Success = false,
            AddressedVia = [.. userAddressedVia.TryGetValue(user, out List<string>? via) ? via : []]
        }));

        return (messageId, allResults);
    }

    /// <summary>
    /// Sends <paramref name="message"/> once to <paramref name="externalServer"/> — regardless of how many
    /// remote users it is addressed to — instead of dialing each one individually over the peer network,
    /// since <see cref="Control.IEngineController.ExternalServer"/> designates it as the exclusive upstream
    /// hub for every message this instance sends. Every remote recipient shares that single send's outcome.
    /// </summary>
    private async Task<UserDeliveryResult[]> RouteToExternalServer(IExternalSystem externalServer, string messageId, object message, List<string> remoteUsers, Dictionary<string, List<string>> userAddressedVia)
    {
        if (remoteUsers.Count == 0) { return []; }

        bool sent = await externalServer.Send(message);
        logger.LogInformation(
            sent ? "{MessageId} delivered to external server for {Count} recipient(s)" : "{MessageId} failed to reach external server for {Count} recipient(s)",
            messageId, remoteUsers.Count);

        return [.. remoteUsers.Select(user =>
        {
            IReadOnlyList<string> via = userAddressedVia.TryGetValue(user, out List<string>? v) ? v.AsReadOnly() : Array.Empty<string>();
            return new UserDeliveryResult { UserName = user, Success = sent, AddressedVia = [.. via] };
        })];
    }
}
