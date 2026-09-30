namespace BlueHeighliner.Comlink.Data.Repositories;

/// <summary>Provides data-access operations for a storage server's <see cref="StoredMessageEntity"/> documents.</summary>
internal interface IStoredMessageRepository
{
    /// <summary>Stores <paramref name="entity"/> unless a document with the same <see cref="StoredMessageEntity.MessageId"/> already exists; returns whether it was stored.</summary>
    Task<bool> InsertIfNew(StoredMessageEntity entity);
    /// <summary>Returns every stored message document.</summary>
    Task<List<StoredMessageEntity>> GetAll();
}

/// <summary>Provides data-access operations for a storage server's <see cref="StoredMessageEntity"/> documents.</summary>
internal sealed class StoredMessageRepository : IStoredMessageRepository
{
    /// <summary>Initializes a new <see cref="StoredMessageRepository"/> backed by the given database context.</summary>
    public StoredMessageRepository(ILiteDbContext ctx) => this.ctx = ctx;

    private readonly ILiteDbContext ctx;
    private readonly SemaphoreSlim insertLock = new(1, 1);

    /// <inheritdoc />
    public async Task<bool> InsertIfNew(StoredMessageEntity entity)
    {
        await insertLock.WaitAsync();
        try
        {
            ctx.Initialize();
            if (ctx.StoredMessages.Exists(m => m.MessageId == entity.MessageId)) { return false; }
            ctx.StoredMessages.Insert(entity);
            return true;
        }
        finally
        {
            insertLock.Release();
        }
    }

    /// <inheritdoc />
    public Task<List<StoredMessageEntity>> GetAll() => Task.Run(() =>
    {
        ctx.Initialize();
        return ctx.StoredMessages.FindAll().ToList();
    });
}
