namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Provides the root application data directory path.</summary>
public interface IAppDataPathProvider
{
    /// <summary>Absolute path to the application data directory.</summary>
    string AppDataPath { get; }
}

/// <summary>Implements <see cref="IAppDataPathProvider"/> using the data folder value from engine configuration, defaulting to <c>%APPDATA%\{AppName}</c>.</summary>
internal sealed class AppDataPathProvider : IAppDataPathProvider
{
    /// <summary>Initializes a new instance resolving the data folder from the configured value.</summary>
    /// <param name="appNameProvider">Provides the application name used to construct the default path.</param>
    /// <param name="config">Engine configuration providing a data folder override.</param>
    public AppDataPathProvider(IAppNameProvider appNameProvider, EngineConfig config)
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appName = appNameProvider.AppName;
        AppDataPath = config.DataFolder switch
        {
            null => Path.Combine(appData, appName),
            ['@', ..] => Path.Combine(appData, appName, config.DataFolder[1..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            _ => config.DataFolder
        };
    }

    /// <inheritdoc />
    public string AppDataPath { get; }
}
