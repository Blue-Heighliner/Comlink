namespace BlueHeighliner.Comlink.Engine.Data.Repositories;

/// <summary>Provides data-access operations for <see cref="ActivityLogEntity"/> documents.</summary>
public interface IActivityLogRepository
{
    /// <summary>Returns a page of activity log entries ordered by date descending.</summary>
    Task<List<ActivityLogEntity>> GetPage(int page);
    /// <summary>Returns the total number of activity log documents.</summary>
    Task<int> Count();
    /// <summary>Returns the activity log document for today's UTC date, or <c>null</c> if none exists.</summary>
    Task<ActivityLogEntity?> GetForToday();
    /// <summary>Returns the activity log document with the given identifier, or <c>null</c> if not found.</summary>
    Task<ActivityLogEntity?> Get(ObjectId id);
    /// <summary>Inserts a new activity log document and returns it.</summary>
    Task<ActivityLogEntity> Insert(ActivityLogEntity entity);
    /// <summary>Persists changes to an existing activity log document.</summary>
    Task Update(ActivityLogEntity entity);
    /// <summary>Appends an event string to today's activity log, creating the document if necessary.</summary>
    Task AppendEvent(string eventText);
}

/// <summary>Provides data-access operations for <see cref="ActivityLogEntity"/> documents.</summary>
public sealed class ActivityLogRepository : IActivityLogRepository
{
    private readonly ILiteDbContext _ctx;
    private readonly SemaphoreSlim _appendLock = new(1, 1);
    private const int PageSize = 50;

    /// <summary>Initializes a new <see cref="ActivityLogRepository"/> backed by the given database context.</summary>
    public ActivityLogRepository(ILiteDbContext ctx) => _ctx = ctx;

    /// <inheritdoc />
    public Task<List<ActivityLogEntity>> GetPage(int page) =>
        Task.Run(() => _ctx.ActivityLogs
            .Query()
            .OrderByDescending(a => a.Date)
            .Skip((page - 1) * PageSize)
            .Limit(PageSize)
            .ToList());

    /// <inheritdoc />
    public Task<int> Count() =>
        Task.Run(() => _ctx.ActivityLogs.Count());

    /// <inheritdoc />
    public Task<ActivityLogEntity?> GetForToday() =>
        Task.Run<ActivityLogEntity?>(() =>
        {
            DateTime today = DateTime.UtcNow.Date;
            return _ctx.ActivityLogs.FindOne(a => a.Date == today);
        });

    /// <inheritdoc />
    public Task<ActivityLogEntity?> Get(ObjectId id) =>
        Task.Run<ActivityLogEntity?>(() => _ctx.ActivityLogs.FindById(id));

    /// <inheritdoc />
    public Task<ActivityLogEntity> Insert(ActivityLogEntity entity) =>
        Task.Run(() => { _ctx.ActivityLogs.Insert(entity); return entity; });

    /// <inheritdoc />
    public Task Update(ActivityLogEntity entity) =>
        Task.Run(() => _ctx.ActivityLogs.Update(entity));

    /// <inheritdoc />
    public async Task AppendEvent(string eventText)
    {
        await _appendLock.WaitAsync();
        try
        {
            DateTime today = DateTime.UtcNow.Date;
            ActivityLogEntry entry = new() { At = DateTime.UtcNow, Message = eventText };
            ActivityLogEntity? log = _ctx.ActivityLogs.FindOne(a => a.Date == today);
            if (log is null)
            {
                log = new ActivityLogEntity { Date = today, EventEntries = [entry] };
                _ctx.ActivityLogs.Insert(log);
            }
            else
            {
                log.EventEntries.Add(entry);
                _ctx.ActivityLogs.Update(log);
            }
        }
        finally
        {
            _appendLock.Release();
        }
    }
}
