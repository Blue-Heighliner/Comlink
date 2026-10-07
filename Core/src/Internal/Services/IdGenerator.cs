namespace BlueHeighliner.Comlink;

/// <summary>Generates frame identifiers through the host's <see cref="IMessageHandler{TFrame, TPriority, TLevel, TAspect}.NextId"/>, handing it the identifier generated last, which it keeps between restarts.</summary>
internal interface IIdGenerator
{
    /// <summary>Generates the next identifier and remembers it as the last one.</summary>
    Task<string> Next();
}

/// <inheritdoc cref="IIdGenerator" />
internal sealed class IdGenerator : IIdGenerator
{
    /// <summary>Initializes a new <see cref="IdGenerator"/>.</summary>
    public IdGenerator(IEngineController engineController, ILastIdRepository repository)
    {
        this.engineController = engineController;
        this.repository = repository;
    }

    private readonly IEngineController engineController;
    private readonly ILastIdRepository repository;
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? last;
    private bool loaded;

    /// <inheritdoc />
    public async Task<string> Next()
    {
        await gate.WaitAsync();
        try
        {
            if (!loaded)
            {
                last = await repository.Get();
                loaded = true;
            }

            string id = engineController.NextId(last);
            await repository.Save(id);
            last = id;
            return id;
        }
        finally
        {
            gate.Release();
        }
    }
}
