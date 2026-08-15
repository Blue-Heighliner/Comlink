namespace BlueHeighliner.Comlink.Engine.Data.Repositories;

/// <summary>Provides data-access operations for <see cref="MessageEntity"/> documents.</summary>
public interface IMessageRepository
{
    /// <summary>Returns a page of messages in the specified folder ordered by received date descending.</summary>
    Task<List<MessageEntity>> GetPage(string folderId, int page);
    /// <summary>Returns the count of messages in the specified folder.</summary>
    Task<int> Count(string folderId);
    /// <summary>Returns every message document in the database, both Inbox and Outbox, across all folders.</summary>
    Task<List<MessageEntity>> GetAll();
    /// <summary>
    /// Returns the message with the given application-level identifier and direction, or <c>null</c> if not found.
    /// A self-addressed message has both an inbound (received) and outbound (sent) document sharing the same
    /// <paramref name="messageId"/>, so <paramref name="outbound"/> disambiguates which one to return.
    /// </summary>
    Task<MessageEntity?> Get(string messageId, bool outbound);
    /// <summary>Inserts a new message document.</summary>
    Task Insert(MessageEntity entity);
    /// <summary>Persists changes to an existing message document.</summary>
    Task Update(MessageEntity entity);
    /// <summary>Deletes the message document with the given application-level identifier and direction. See <see cref="Get"/> for why direction is required.</summary>
    Task Delete(string messageId, bool outbound);
}

/// <summary>Provides data-access operations for <see cref="MessageEntity"/> documents.</summary>
public sealed class MessageRepository : IMessageRepository
{
    private const int PageSize = 50;

    /// <summary>Initializes a new <see cref="MessageRepository"/> backed by the given database context.</summary>
    public MessageRepository(ILiteDbContext ctx) => this.ctx = ctx;

    private readonly ILiteDbContext ctx;

    /// <inheritdoc />
    public Task<List<MessageEntity>> GetPage(string folderId, int page)
        => Task.Run(() => ctx.Messages
            .Query()
            .Where(m => m.FolderId == folderId)
            .OrderByDescending(m => m.ReceivedAt)
            .Skip((page - 1) * PageSize)
            .Limit(PageSize)
            .ToList());

    /// <inheritdoc />
    public Task<int> Count(string folderId)
        => Task.Run(() => ctx.Messages.Count(m => m.FolderId == folderId));

    /// <inheritdoc />
    public Task<List<MessageEntity>> GetAll()
        => Task.Run(() => ctx.Messages.FindAll().ToList());

    /// <inheritdoc />
    public Task<MessageEntity?> Get(string messageId, bool outbound)
        => Task.Run<MessageEntity?>(() => ctx.Messages.FindOne(m => m.MessageId == messageId && m.IsOutbound == outbound));

    /// <inheritdoc />
    public Task Insert(MessageEntity entity)
        => Task.Run(() => ctx.Messages.Insert(entity));

    /// <inheritdoc />
    public Task Update(MessageEntity entity)
        => Task.Run(() => ctx.Messages.Update(entity));

    /// <inheritdoc />
    public Task Delete(string messageId, bool outbound)
        => Task.Run(() => ctx.Messages.DeleteMany(m => m.MessageId == messageId && m.IsOutbound == outbound));
}
