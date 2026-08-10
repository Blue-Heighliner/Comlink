namespace BlueHeighliner.Comlink.Engine.Data.Repositories;

/// <summary>Provides data-access operations for <see cref="FolderEntity"/> documents.</summary>
public interface IFolderRepository
{
    /// <summary>Returns all folder documents in the database.</summary>
    Task<List<FolderEntity>> GetAll();
    /// <summary>Returns the folder with the given identifier, or <c>null</c> if not found.</summary>
    Task<FolderEntity?> Get(string id);
    /// <summary>Returns the well-known root folder identifier for the given folder type.</summary>
    Task<string> GetRootId(FolderType type);
    /// <summary>Inserts a new folder document and returns it.</summary>
    Task<FolderEntity> Insert(FolderEntity entity);
    /// <summary>Deletes the folder with the given identifier and returns <c>true</c> if it was found.</summary>
    Task<bool> Delete(string id);
    /// <summary>Returns the full folder hierarchy as a tree of <see cref="Folder"/> models.</summary>
    Task<List<Folder>> GetTree();
}

/// <summary>Provides data-access operations for <see cref="FolderEntity"/> documents.</summary>
public sealed class FolderRepository : IFolderRepository
{
    private readonly ILiteDbContext _ctx;

    /// <summary>Initializes a new <see cref="FolderRepository"/> backed by the given database context.</summary>
    public FolderRepository(ILiteDbContext ctx) => _ctx = ctx;

    /// <inheritdoc />
    public Task<List<FolderEntity>> GetAll() =>
        Task.Run(() => _ctx.Folders.FindAll().ToList());

    /// <inheritdoc />
    public Task<FolderEntity?> Get(string id) =>
        Task.Run<FolderEntity?>(() => _ctx.Folders.FindById(id));

    /// <inheritdoc />
    public Task<string> GetRootId(FolderType type) =>
        Task.FromResult($"root-{type.ToString().ToLower()}");

    /// <inheritdoc />
    public Task<FolderEntity> Insert(FolderEntity entity) =>
        Task.Run(() => { _ctx.Folders.Insert(entity); return entity; });

    /// <inheritdoc />
    public Task<bool> Delete(string id) =>
        Task.Run(() => _ctx.Folders.Delete(id));

    /// <inheritdoc />
    public Task<List<Folder>> GetTree() =>
        Task.Run(() =>
        {
            List<FolderEntity> all = _ctx.Folders.FindAll().ToList();
            return BuildTree(all, null);
        });

    private static List<Folder> BuildTree(List<FolderEntity> all, string? parentId)
    {
        return all
            .Where(f => f.ParentId == parentId)
            .Select(f => new Folder
            {
                Id = f.Id,
                Name = f.Name,
                RootType = f.RootType,
                ParentId = f.ParentId,
                Children = BuildTree(all, f.Id)
            })
            .ToList();
    }
}
