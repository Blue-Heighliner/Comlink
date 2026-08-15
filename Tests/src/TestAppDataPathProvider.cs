namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test implementation of <see cref="IEngineController"/> that uses a GUID-named directory under <c>%APPDATA%</c> for test isolation.</summary>
internal sealed class TestAppDataPathProvider : TestEngineController
{
    /// <summary>Initializes the provider with a path derived from the given app name.</summary>
    public TestAppDataPathProvider(string appName)
    {
        AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            appName);
    }

    /// <inheritdoc />
    public override string AppDataPath { get; }
}
