namespace BlueHeighliner.Comlink;

/// <summary>
/// The storage half of a server: keeps the messages the host's network processor tells it to keep, and finds the kept messages that fit the criteria of a retrieval. The engine neither decides what is kept nor who may retrieve
/// it, nor sends what is found: those are the processor's, through its context (see <see cref="INetworkContext{TFrame, TPriority, TLevel, TAspect}.StoreMessage"/> and <see cref="INetworkContext{TFrame, TPriority, TLevel, TAspect}.FindStoredMessages"/>).
/// </summary>
internal interface IMessageStorageService
{
    /// <summary>Keeps a copy of <paramref name="message"/> unless one with the same identifier is already kept. A failure is logged, never thrown: storage must not interrupt the processor.</summary>
    /// <param name="message">The message to keep.</param>
    Task Store(Message message);

    /// <summary>Finds the kept messages fitting <paramref name="criteria"/>, whoever sent or received them, ordered by sent time. A failure is logged and finds nothing.</summary>
    /// <param name="criteria">What the messages must fit.</param>
    Task<IReadOnlyList<Message>> Find(RetrievalCriteria criteria);
}

/// <inheritdoc cref="IMessageStorageService" />
internal sealed class MessageStorageService : IMessageStorageService
{
    /// <summary>Initializes a new <see cref="MessageStorageService"/>.</summary>
    public MessageStorageService(IStoredMessageRepository repository, IEngineController engineController, ILoggerFactory loggerFactory)
    {
        this.repository = repository;
        this.engineController = engineController;
        logger = loggerFactory.CreateLogger(LogCategories.App);
    }

    private readonly IStoredMessageRepository repository;
    private readonly IEngineController engineController;
    private readonly ILogger logger;

    /// <inheritdoc />
    public async Task Store(Message message)
    {
        try
        {
            await repository.InsertIfNew(new StoredMessageEntity { MessageId = message.Id, Message = engineController.ToData(message) });
        }
        catch (Exception ex)
        {
            logger.Record(LogEvents.StoreMessageCopyFailed, ex, "Failed to store a copy of {MessageId}", message.Id);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Message>> Find(RetrievalCriteria criteria)
    {
        try
        {
            List<Message> found = [.. (await repository.GetAll())
                .Select(entity => entity.Message)
                .Where(message => Fits(message, criteria))
                .OrderBy(message => Utc(message.SentAt))
                .Select(engineController.ToMessage)];
            logger.Record(LogEvents.RetrievalAnswered, "Retrieval found {Count} stored message(s)", found.Count);
            return found;
        }
        catch (Exception ex)
        {
            logger.Record(LogEvents.ReadStoredMessagesFailed, ex, "Failed to read stored messages for a retrieval");
            return [];
        }
    }

    // A time with no stated kind is taken to be UTC, which is what every sent time and retrieval bound is written as; a local one (LiteDB reads stored times back as local) is converted.
    private static DateTime Utc(DateTime time) => time.Kind is DateTimeKind.Unspecified ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : time.ToUniversalTime();

    private static bool Fits(MessageData message, RetrievalCriteria criteria)
    {
        DateTime sentAt = Utc(message.SentAt);
        if (criteria.From is { } from && sentAt < Utc(from))
        {
            return false;
        }
        if (criteria.To is { } to && sentAt > Utc(to))
        {
            return false;
        }
        if (criteria.Ids.Count > 0 && !criteria.Ids.Contains(message.Id, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }
        if (criteria.Authors.Count > 0 && !criteria.Authors.Contains(message.FromUser, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }
        return criteria.Destinations.Count == 0 || message.Addresses.Any(address => criteria.Destinations.Contains(address.UserName, StringComparer.OrdinalIgnoreCase));
    }
}
