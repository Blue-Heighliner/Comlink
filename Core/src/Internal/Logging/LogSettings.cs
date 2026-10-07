namespace BlueHeighliner.Comlink;

/// <summary>Says which log categories are written. The categories that are off by default (see <see cref="LogCategories.OffByDefault"/>) are turned on by <c>Logging.json</c> and the <c>--log</c> argument.</summary>
internal interface ILogSettings
{
    /// <summary>Returns whether entries of <paramref name="category"/> are written. A category the engine has no switch for, such as a host's own, always is.</summary>
    /// <param name="category">The category, in any case.</param>
    bool IsEnabled(string category);

    /// <summary>Reads <c>Logging.json</c> again and applies it with the categories named on the command line.</summary>
    /// <exception cref="InvalidDataException">The file exists but cannot be read as JSON; the categories enabled so far stay as they were.</exception>
    void Reload();
}

/// <summary>
/// The categories enabled beyond the defaults, from <c>Logging.json</c> beside <c>User.json</c> in the app data folder (a JSON list of category names, <c>["Frames", "Packets"]</c>)
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
        try { Reload(); }
        catch (InvalidDataException) { }
    }

    private readonly IEngineController engineController;
    private readonly NetworkConfig network;
    private volatile HashSet<string> enabled = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public bool IsEnabled(string category) => !LogCategories.OffByDefault.Contains(category, StringComparer.OrdinalIgnoreCase) || enabled.Contains(category);

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
