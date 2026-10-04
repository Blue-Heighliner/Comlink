namespace BlueHeighliner.Comlink;

/// <summary>Provides data-access operations for the one <see cref="LastIdEntity"/> document.</summary>
internal interface ILastIdRepository
{
    /// <summary>Returns the last identifier generated, or <see langword="null"/> if none has been saved yet.</summary>
    Task<string?> Get();
    /// <summary>Saves <paramref name="id"/> as the last identifier generated.</summary>
    Task Save(string id);
}

/// <inheritdoc cref="ILastIdRepository" />
internal sealed class LastIdRepository : ILastIdRepository
{
    /// <summary>Initializes a new <see cref="LastIdRepository"/> backed by the given database context.</summary>
    public LastIdRepository(ILiteDbContext ctx) => this.ctx = ctx;

    private readonly ILiteDbContext ctx;

    /// <inheritdoc />
    public Task<string?> Get() => Task.Run<string?>(() =>
    {
        ctx.Initialize();
        return ctx.LastIds.FindById(1)?.Value;
    });

    /// <inheritdoc />
    public Task Save(string id) => Task.Run(() =>
    {
        ctx.Initialize();
        ctx.LastIds.Upsert(new LastIdEntity { Id = 1, Value = id });
    });
}
