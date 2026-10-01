namespace BlueHeighliner.Comlink.Services;

/// <summary>
/// The storage half of a storage server (see <see cref="UserInfo.StoresMessages"/>): keeps a copy of each
/// message the server routes, and answers a retrieval request by finding the stored messages that fit its criteria.
/// Any user can retrieve any stored message; nothing restricts a request to the requester's own traffic. Sending the found copies is left to the caller, since only the server's peer service
/// knows how to reach the requester and this service must not depend on it.
/// </summary>
internal interface IMessageStorageService
{
    /// <summary>Gets a value indicating whether the current user is one of <see cref="IEngineController.StorageServers"/>, so <see cref="Store"/> keeps messages and <see cref="Find"/> can answer.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Keeps a copy of <paramref name="message"/> unless one with the same identifier is already stored, or
    /// storage is not <see cref="IsEnabled"/>, or the message is a receipt or a retrieval request (neither is
    /// user content). A failure is logged, never thrown: storage must not interrupt routing.
    /// </summary>
    Task Store(object message);

    /// <summary>
    /// Finds the stored messages fitting the criteria in <paramref name="request"/>, whoever sent or received them,
    /// and returns a copy of each addressed to <paramref name="requester"/> alone, ordered by sent time. A copy keeps the original's
    /// identifier, sender, sent time, body, priority, tag and security level, but is never an alert (so old
    /// alerts do not alarm again) and carries only the requester as its address, since servers route purely by
    /// address list. Returns an empty list when storage is not enabled, or the request's criteria are unreadable.
    /// </summary>
    Task<IReadOnlyList<object>> Find(string requester, object request);
}

/// <inheritdoc cref="IMessageStorageService" />
internal sealed class MessageStorageService : IMessageStorageService
{
    /// <summary>Initializes a new <see cref="MessageStorageService"/>.</summary>
    public MessageStorageService(
        IStoredMessageRepository repository,
        IEngineController engineController,
        ICurrentUserProvider currentUserProvider,
        ILoggerFactory loggerFactory)
    {
        this.repository = repository;
        this.engineController = engineController;
        this.currentUserProvider = currentUserProvider;
        logger = loggerFactory.CreateLogger("ACTIVITY");
    }

    private readonly IStoredMessageRepository repository;
    private readonly IEngineController engineController;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly ILogger logger;

    /// <inheritdoc />
    public bool IsEnabled => currentUserProvider.UserName is { } name && engineController.StorageServers.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task Store(object message)
    {
        if (!IsEnabled || !engineController.IsMessage(message)) { return; }
        if (engineController.IsReadReceipt(message) || engineController.IsReceiveReceipt(message) || engineController.IsRetrieval(message)) { return; }

        try
        {
            await repository.InsertIfNew(new StoredMessageEntity { MessageId = engineController.GetFrameId(message), Message = message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store a copy of {MessageId}", engineController.GetFrameId(message));
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<object>> Find(string requester, object request)
    {
        if (!IsEnabled) { return []; }

        RetrievalCriteria criteria = engineController.GetRetrieval(request);

        try
        {
            List<object> copies = [.. (await repository.GetAll())
                .Select(entity => entity.Message)
                .Where(message => Fits(message, criteria))
                .OrderBy(message => Utc(engineController.GetSentAt(message)))
                .Select(message => CopyFor(message, requester))];
            logger.LogInformation("Retrieval for {Requester} found {Count} stored message(s)", requester, copies.Count);
            return copies;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read stored messages for a retrieval by {Requester}", requester);
            return [];
        }
    }

    // A time with no stated kind (as a serializer may hand back) is taken to be UTC, which is what every sent time and
    // retrieval bound is written as; a local one (LiteDB reads stored times back as local) is converted.
    private DateTime Utc(DateTime time) => time.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : time.ToUniversalTime();

    private bool Fits(object message, RetrievalCriteria criteria)
    {
        // LiteDB reads a stored DateTime back as local time, so compare as UTC instants.
        DateTime sentAt = Utc(engineController.GetSentAt(message));
        if (criteria.From is { } from && sentAt < Utc(from)) { return false; }
        if (criteria.To is { } to && sentAt > Utc(to)) { return false; }
        if (criteria.Ids.Count > 0 && !criteria.Ids.Contains(engineController.GetFrameId(message), StringComparer.OrdinalIgnoreCase)) { return false; }
        if (criteria.Authors.Count > 0 && !criteria.Authors.Contains(engineController.GetFromUser(message), StringComparer.OrdinalIgnoreCase)) { return false; }
        return criteria.Destinations.Count == 0
            || engineController.GetAddresses(message).Any(address => criteria.Destinations.Contains(address.UserName, StringComparer.OrdinalIgnoreCase));
    }

    private object CopyFor(object original, string requester)
    {
        object copy = engineController.CreateMessage(new MessageCreateContext
        {
            Body = engineController.GetBody(original),
            IsAlert = false,
            Priority = engineController.GetPriority(original),
            Tag = engineController.GetTag(original),
            SecurityLevel = engineController.GetSecurityLevel(original)
        });
        engineController.SetFrameId(copy, engineController.GetFrameId(original));
        engineController.SetFromUser(copy, engineController.GetFromUser(original));
        engineController.SetSentAt(copy, Utc(engineController.GetSentAt(original)));
        engineController.SetAddresses(copy, [new MessageAddress { UserName = requester, Type = AddressType.To }]);
        return copy;
    }
}
