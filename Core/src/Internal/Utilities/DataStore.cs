namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="IDataStore" />
/// <param name="engineController">Supplies the current user's data folder.</param>
internal sealed class DataStore(IEngineController engineController) : IDataStore
{
    private readonly JsonSerializerOptions options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

    /// <inheritdoc />
    public async Task Save<T>(string id, T value, CancellationToken cancellation = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        string path = PathOf(id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, value, options, cancellation);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T?> Load<T>(string id, CancellationToken cancellation = default) where T : class
    {
        FileStream stream;
        try
        {
            stream = new FileStream(PathOf(id), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }

        await using (stream)
        {
            try
            {
                return await JsonSerializer.DeserializeAsync<T>(stream, options, cancellation);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"The data stored under \"{id}\" cannot be read as {typeof(T).Name}: {ex.Message}", ex);
            }
        }
    }

    private string PathOf(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        StringBuilder name = new();
        foreach (byte part in Encoding.UTF8.GetBytes(id.ToLowerInvariant()))
        {
            if (part is (>= (byte)'a' and <= (byte)'z') or (>= (byte)'0' and <= (byte)'9') or (byte)'_' or (byte)'-')
            {
                name.Append((char)part);
            }
            else
            {
                name.Append('%').Append(part.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return Path.Combine(engineController.AppDataPath, "Store", name + ".json");
    }
}
