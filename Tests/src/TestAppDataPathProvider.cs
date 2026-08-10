namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test implementation of <see cref="IAppDataPathProvider"/> that uses a GUID-named directory under <c>%APPDATA%</c> for test isolation.</summary>
internal sealed class TestAppDataPathProvider : IAppDataPathProvider
{
    /// <summary>Initializes the provider with a path derived from the given app name.</summary>
    public TestAppDataPathProvider(string appName)
    {
        AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            appName);
    }

    /// <inheritdoc />
    public string AppDataPath { get; }
}
