namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="IDataStore"/> service that keeps records in memory, by identifier whatever the case, and hands back the instance that was saved. Like the engine's, it refuses an empty identifier and refuses to read a record as a type it was not saved as.</summary>
public sealed class FakeDataStore : IDataStore
{
    /// <summary>Gets the stored records by identifier, which a test can fill to set up what a component finds or read to see what it saved.</summary>
    public Dictionary<string, object> Items { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task Save<T>(string id, T value, CancellationToken cancellation = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(value);
        Items[id] = value;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<T?> Load<T>(string id, CancellationToken cancellation = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Items.TryGetValue(id, out object? item))
        {
            return Task.FromResult<T?>(null);
        }

        return item is T typed ? Task.FromResult<T?>(typed) : throw new InvalidDataException($"The data stored under \"{id}\" cannot be read as {typeof(T).Name}.");
    }
}
