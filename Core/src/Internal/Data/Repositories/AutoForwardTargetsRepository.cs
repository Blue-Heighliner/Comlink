namespace BlueHeighliner.Comlink.Data.Repositories;

/// <summary>Provides data-access operations for a controller's <see cref="AutoForwardTargetsEntity"/> document.</summary>
internal interface IAutoForwardTargetsRepository
{
    /// <summary>Returns the target-list document for <paramref name="controllerName"/>, or <c>null</c> if none has been saved yet.</summary>
    Task<AutoForwardTargetsEntity?> Get(string controllerName);
    /// <summary>Replaces the saved target list for <paramref name="controllerName"/>, creating its document if none exists yet.</summary>
    Task Save(string controllerName, List<string> targets);
}

/// <summary>Provides data-access operations for a controller's <see cref="AutoForwardTargetsEntity"/> document.</summary>
internal sealed class AutoForwardTargetsRepository : IAutoForwardTargetsRepository
{
    /// <summary>Initializes a new <see cref="AutoForwardTargetsRepository"/> backed by the given database context.</summary>
    public AutoForwardTargetsRepository(ILiteDbContext ctx) => this.ctx = ctx;

    private readonly ILiteDbContext ctx;

    /// <inheritdoc />
    public Task<AutoForwardTargetsEntity?> Get(string controllerName)
        => Task.Run<AutoForwardTargetsEntity?>(() => ctx.AutoForwardTargets.FindById(controllerName));

    /// <inheritdoc />
    public Task Save(string controllerName, List<string> targets)
        => Task.Run(() => ctx.AutoForwardTargets.Upsert(new AutoForwardTargetsEntity { Id = controllerName, Targets = targets }));
}
