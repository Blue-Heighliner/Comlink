namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IAppDataPathProvider"/> that reproduces the Engine default resolution exactly, plus an
/// additional <c>DATA_FOLDER</c> environment variable fallback (same absolute-path/<c>@</c>-prefix rules as
/// <c>config.json</c>'s <c>DataFolder</c>) used only when <c>config.json</c> does not set one.
/// </summary>
public sealed class SampleAppDataPathProvider : IAppDataPathProvider
{
    /// <summary>Initializes a new <see cref="SampleAppDataPathProvider"/> resolving the data folder from config, then the <c>DATA_FOLDER</c> environment variable, then the default.</summary>
    /// <param name="appNameProvider">Provides the application name used to construct the default path.</param>
    /// <param name="config">Engine configuration providing a data folder override.</param>
    public SampleAppDataPathProvider(IAppNameProvider appNameProvider, EngineConfig config)
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appName = appNameProvider.AppName;
        string? dataFolder = config.DataFolder ?? Environment.GetEnvironmentVariable("DATA_FOLDER");
        AppDataPath = dataFolder switch
        {
            null => Path.Combine(appData, appName),
            ['@', ..] => Path.Combine(appData, appName, dataFolder[1..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            _ => dataFolder
        };
    }

    /// <inheritdoc />
    public string AppDataPath { get; }
}
