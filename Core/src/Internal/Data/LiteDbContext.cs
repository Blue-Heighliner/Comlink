namespace BlueHeighliner.Comlink.Data;

/// <summary>Provides access to the LiteDB database and all typed entity collections.</summary>
internal interface ILiteDbContext : IDisposable
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
internal sealed class LiteDbContext : ILiteDbContext
{
    /// <summary>Initializes a new <see cref="LiteDbContext"/> using the provided path provider.</summary>
    public LiteDbContext(IEngineController engineController)
    {
        this.engineController = engineController;
    }

    private static readonly Lock mapperWarmupLock = new();

    private readonly IEngineController engineController;
    private LiteDatabase? db;

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


    /// <summary>Opens the database file, binds all collections, and ensures indexes and root folders exist.</summary>
    public void Initialize()
    {
        db?.Dispose();
        WarmUpMapper();
        string dataDir = engineController.AppDataPath;
        Directory.CreateDirectory(dataDir);
        db = new LiteDatabase(Path.Combine(dataDir, "Data.db"));

        Messages = db.GetCollection<MessageEntity>("messages");
        Drafts = db.GetCollection<DraftEntity>("drafts");
        Notes = db.GetCollection<NoteEntity>("notes");
        ActivityLogs = db.GetCollection<ActivityLogEntity>("activity_logs");
        Folders = db.GetCollection<FolderEntity>("folders");

        EnsureIndexes();
        EnsureRootFolders();
    }

    // LiteDB's shared BsonMapper publishes a type's mapper before it has finished building it, so two threads
    // serializing a type for the first time can collide ("Collection was modified"), e.g. a message arriving while a
    // draft is saved. Serializing one fully populated instance of every stored shape here, once, under a lock, builds
    // every mapper up front, including nested list element types and the host's own message type.
    private void WarmUpMapper()
    {
        lock (mapperWarmupLock)
        {
            object message = engineController.CreateMessage();
            engineController.SetAddresses(message, [new MessageAddress { UserName = string.Empty, Type = AddressType.To }]);
            BsonMapper mapper = BsonMapper.Global;
            mapper.ToDocument(new MessageEntity
            {
                MessageId = string.Empty,
                Message = message,
                DeliveryStatuses = [new DeliveryStatus { UserName = string.Empty, AddressedVia = [string.Empty] }]
            });
            mapper.ToDocument(new DraftEntity { Addresses = [new AddressData { UserName = string.Empty, Type = string.Empty, Information = string.Empty }] });
            mapper.ToDocument(new NoteEntity());
            mapper.ToDocument(new ActivityLogEntity { Events = [string.Empty], EventEntries = [new ActivityLogEntry()] });
            mapper.ToDocument(new FolderEntity { Id = string.Empty, Name = string.Empty });
        }
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
    public void Dispose() => db?.Dispose();
}
