namespace BlueHeighliner.Comlink;

/// <summary>Says which log categories are written. Only the categories that are on by default (the log handler's, or else <see cref="LogCategories.OnByDefault"/>) are written; every other is turned on by <c>Logging.json</c> and the <c>--log</c> argument.</summary>
internal interface ILogSettings
{
    /// <summary>Returns whether entries of <paramref name="category"/> are written: the categories on by default, and any other that <c>Logging.json</c> or the <c>--log</c> argument names.</summary>
    /// <param name="category">The category, in any case.</param>
    bool IsEnabled(string category);

    /// <summary>Reads <c>Logging.json</c> again and applies it with the categories named on the command line.</summary>
    /// <exception cref="InvalidDataException">The file exists but cannot be read as JSON; the categories enabled so far stay as they were.</exception>
    void Reload();
}

/// <summary>
/// The categories enabled beyond the defaults, from <c>Logging.json</c> beside <c>Config.json</c> in the app data folder (a JSON list of category names, <c>["Frames", "Packets"]</c>)
/// and from the <c>--log</c> argument (a comma separated list), when the host allows command-line overrides.
/// </summary>
internal sealed class LogSettings : ILogSettings
{
    /// <summary>Initializes the settings from the file and the command line. A file that cannot be read leaves the defaults, since nothing can be logged about it yet.</summary>
    /// <param name="engineController">Says where <c>Logging.json</c> is.</param>
    /// <param name="network">Holds the categories named on the command line.</param>
    public LogSettings(IEngineController engineController, NetworkConfig network)
    {
        this.engineController = engineController;
        this.network = network;
        onByDefault = new(() => new HashSet<string>(engineController.LogCategoriesOnByDefault, StringComparer.OrdinalIgnoreCase));
        try { Reload(); }
        catch (InvalidDataException) { }
    }

    private readonly IEngineController engineController;
    private readonly NetworkConfig network;
    private readonly Lazy<HashSet<string>> onByDefault;
    private volatile HashSet<string> enabled = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public bool IsEnabled(string category) => onByDefault.Value.Contains(category) || enabled.Contains(category);

    /// <inheritdoc />
    public void Reload()
    {
        HashSet<string> fresh = new(network.EnabledLogCategories, StringComparer.OrdinalIgnoreCase);
        string path = engineController.LoggingFilePath;
        if (File.Exists(path))
        {
            try
            {
                fresh.UnionWith(JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? []);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Logging.json is not valid: {ex.Message}", ex);
            }
        }

        enabled = fresh;
    }
}
