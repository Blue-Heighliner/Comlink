namespace BlueHeighliner.Comlink.Engine.Data;

/// <summary>Provides access to the LiteDB database and all typed entity collections.</summary>
public interface ILiteDbContext : IDisposable
{
    /// <summary>Collection of persisted messages.</summary>
    ILiteCollection<MessageEntity> Messages { get; }
    /// <summary>Collection of persisted drafts.</summary>
    ILiteCollection<DraftEntity> Drafts { get; }
    /// <summary>Collection of persisted notes.</summary>
    ILiteCollection<NoteEntity> Notes { get; }
    /// <summary>Collection of persisted daily activity logs.</summary>
    ILiteCollection<ActivityLogEntity> ActivityLogs { get; }
    /// <summary>Collection of persisted folders.</summary>
    ILiteCollection<FolderEntity> Folders { get; }
    /// <summary>Opens the database file, binds all collections, and ensures indexes and root folders exist.</summary>
    void Initialize();
}

/// <summary>Owns the LiteDB connection and exposes typed collections for all Engine entities.</summary>
public sealed class LiteDbContext : ILiteDbContext
{
    private readonly IAppDataPathProvider _appDataPathProvider;
    private LiteDatabase? _db;

    /// <summary>Collection of persisted messages.</summary>
    public ILiteCollection<MessageEntity> Messages { get; private set; } = null!;
    /// <summary>Collection of persisted drafts.</summary>
    public ILiteCollection<DraftEntity> Drafts { get; private set; } = null!;
    /// <summary>Collection of persisted notes.</summary>
    public ILiteCollection<NoteEntity> Notes { get; private set; } = null!;
    /// <summary>Collection of persisted daily activity logs.</summary>
    public ILiteCollection<ActivityLogEntity> ActivityLogs { get; private set; } = null!;
    /// <summary>Collection of persisted folders.</summary>
    public ILiteCollection<FolderEntity> Folders { get; private set; } = null!;

    /// <summary>Initializes a new <see cref="LiteDbContext"/> using the provided path provider.</summary>
    public LiteDbContext(IAppDataPathProvider appDataPathProvider)
    {
        _appDataPathProvider = appDataPathProvider;
    }

    /// <summary>Opens the database file, binds all collections, and ensures indexes and root folders exist.</summary>
    public void Initialize()
    {
        _db?.Dispose();
        string dataDir = _appDataPathProvider.AppDataPath;
        Directory.CreateDirectory(dataDir);
        _db = new LiteDatabase(Path.Combine(dataDir, "Data.db"));

        Messages = _db.GetCollection<MessageEntity>("messages");
        Drafts = _db.GetCollection<DraftEntity>("drafts");
        Notes = _db.GetCollection<NoteEntity>("notes");
        ActivityLogs = _db.GetCollection<ActivityLogEntity>("activity_logs");
        Folders = _db.GetCollection<FolderEntity>("folders");

        EnsureIndexes();
        EnsureRootFolders();
    }

    private void EnsureIndexes()
    {
        Messages.EnsureIndex(x => x.FolderId);
        Messages.EnsureIndex(x => x.ReceivedAt);
        Drafts.EnsureIndex(x => x.FolderId);
        Drafts.EnsureIndex(x => x.ModifiedAt);
        Notes.EnsureIndex(x => x.FolderId);
        Notes.EnsureIndex(x => x.ModifiedAt);
        ActivityLogs.EnsureIndex(x => x.Date);
        Folders.EnsureIndex(x => x.ParentId);
    }

    private void EnsureRootFolders()
    {
        // Remove legacy folder IDs from renamed enum values (delete by ID without deserialization)
        Folders.Delete("root-logs");

        (FolderType, string)[] rootTypes = new[]
        {
            (FolderType.Inbox, "Inbox"),
            (FolderType.Outbox, "Outbox"),
            (FolderType.Drafts, "Drafts"),
            (FolderType.Notes, "Notes"),
            (FolderType.Activity, "Activity")
        };

        foreach ((FolderType type, string name) in rootTypes)
        {
            string id = $"root-{type.ToString().ToLower()}";
            if (Folders.FindById(id) is null)
            {
                Folders.Insert(new FolderEntity
                {
                    Id = id,
                    Name = name,
                    RootType = type,
                    ParentId = null
                });
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _db?.Dispose();
}
