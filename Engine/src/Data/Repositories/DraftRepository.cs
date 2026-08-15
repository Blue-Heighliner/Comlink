namespace BlueHeighliner.Comlink.Engine.Data.Repositories;

/// <summary>Provides data-access operations for <see cref="DraftEntity"/> documents.</summary>
public interface IDraftRepository
{
    /// <summary>Returns a page of unsent drafts in the specified folder, ordered by subject or modified date.</summary>
    Task<List<DraftEntity>> GetPage(string folderId, int page, bool alphabetical);
    /// <summary>Returns the count of unsent drafts in the specified folder.</summary>
    Task<int> Count(string folderId);
    /// <summary>Returns every draft document in the database, sent or unsent, across all folders.</summary>
    Task<List<DraftEntity>> GetAll();
    /// <summary>Returns the draft with the given identifier, or <c>null</c> if not found.</summary>
    Task<DraftEntity?> Get(ObjectId id);
    /// <summary>Inserts a new draft document and returns it.</summary>
    Task<DraftEntity> Insert(DraftEntity entity);
    /// <summary>Persists changes to an existing draft document.</summary>
    Task Update(DraftEntity entity);
    /// <summary>Deletes the draft with the given identifier.</summary>
    Task Delete(ObjectId id);
}

/// <summary>Provides data-access operations for <see cref="DraftEntity"/> documents.</summary>
public sealed class DraftRepository : IDraftRepository
{
    private const int PageSize = 50;

    /// <summary>Initializes a new <see cref="DraftRepository"/> backed by the given database context.</summary>
    public DraftRepository(ILiteDbContext ctx) => this.ctx = ctx;

    private readonly ILiteDbContext ctx;

    /// <inheritdoc />
    public Task<List<DraftEntity>> GetPage(string folderId, int page, bool alphabetical)
        => Task.Run(() =>
        {
            ILiteQueryable<DraftEntity> query = ctx.Drafts.Query().Where(d => d.FolderId == folderId && !d.IsSent);
            return (alphabetical
                ? query.OrderBy(d => d.Subject)
                : query.OrderByDescending(d => d.ModifiedAt))
                .Skip((page - 1) * PageSize)
                .Limit(PageSize)
                .ToList();
        });

    /// <inheritdoc />
    public Task<int> Count(string folderId)
        => Task.Run(() => ctx.Drafts.Count(d => d.FolderId == folderId && !d.IsSent));

    /// <inheritdoc />
    public Task<List<DraftEntity>> GetAll()
        => Task.Run(() => ctx.Drafts.FindAll().ToList());

    /// <inheritdoc />
    public Task<DraftEntity?> Get(ObjectId id)
        => Task.Run<DraftEntity?>(() => ctx.Drafts.FindById(id));

    /// <inheritdoc />
    public Task<DraftEntity> Insert(DraftEntity entity)
        => Task.Run(() => { ctx.Drafts.Insert(entity); return entity; });

    /// <inheritdoc />
    public Task Update(DraftEntity entity)
        => Task.Run(() => ctx.Drafts.Update(entity));

    /// <inheritdoc />
    public Task Delete(ObjectId id)
        => Task.Run(() => ctx.Drafts.Delete(id));
}
