namespace BlueHeighliner.Comlink.Engine.Data.Repositories;

/// <summary>Provides data-access operations for <see cref="NoteEntity"/> documents.</summary>
public interface INoteRepository
{
    /// <summary>Returns a page of notes in the specified folder, ordered by body text or modified date.</summary>
    Task<List<NoteEntity>> GetPage(string folderId, int page, bool alphabetical);
    /// <summary>Returns the count of notes in the specified folder.</summary>
    Task<int> Count(string folderId);
    /// <summary>Returns every note document in the database, across all folders.</summary>
    Task<List<NoteEntity>> GetAll();
    /// <summary>Returns the note with the given identifier, or <c>null</c> if not found.</summary>
    Task<NoteEntity?> Get(ObjectId id);
    /// <summary>Inserts a new note document and returns it.</summary>
    Task<NoteEntity> Insert(NoteEntity entity);
    /// <summary>Persists changes to an existing note document.</summary>
    Task Update(NoteEntity entity);
    /// <summary>Deletes the note with the given identifier.</summary>
    Task Delete(ObjectId id);
}

/// <summary>Provides data-access operations for <see cref="NoteEntity"/> documents.</summary>
public sealed class NoteRepository : INoteRepository
{
    private readonly ILiteDbContext _ctx;
    private const int PageSize = 50;

    /// <summary>Initializes a new <see cref="NoteRepository"/> backed by the given database context.</summary>
    public NoteRepository(ILiteDbContext ctx) => _ctx = ctx;

    /// <inheritdoc />
    public Task<List<NoteEntity>> GetPage(string folderId, int page, bool alphabetical) =>
        Task.Run(() =>
        {
            ILiteQueryable<NoteEntity> query = _ctx.Notes.Query().Where(n => n.FolderId == folderId);
            return (alphabetical
                ? query.OrderBy(n => n.Body)
                : query.OrderByDescending(n => n.ModifiedAt))
                .Skip((page - 1) * PageSize)
                .Limit(PageSize)
                .ToList();
        });

    /// <inheritdoc />
    public Task<int> Count(string folderId) =>
        Task.Run(() => _ctx.Notes.Count(n => n.FolderId == folderId));

    /// <inheritdoc />
    public Task<List<NoteEntity>> GetAll() =>
        Task.Run(() => _ctx.Notes.FindAll().ToList());

    /// <inheritdoc />
    public Task<NoteEntity?> Get(ObjectId id) =>
        Task.Run<NoteEntity?>(() => _ctx.Notes.FindById(id));

    /// <inheritdoc />
    public Task<NoteEntity> Insert(NoteEntity entity) =>
        Task.Run(() => { _ctx.Notes.Insert(entity); return entity; });

    /// <inheritdoc />
    public Task Update(NoteEntity entity) =>
        Task.Run(() => _ctx.Notes.Update(entity));

    /// <inheritdoc />
    public Task Delete(ObjectId id) =>
        Task.Run(() => _ctx.Notes.Delete(id));
}
