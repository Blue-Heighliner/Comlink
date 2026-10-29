namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test implementation of <see cref="IEngineController"/> that uses a named directory under the system temp folder for test isolation, never the application data folder.</summary>
internal sealed class TestAppDataPathProvider : TestEngineController
{
    /// <summary>Initializes the provider with a path derived from the given app name.</summary>
    public TestAppDataPathProvider(string appName) => AppDataPath = DirectoryOf(appName);

    /// <inheritdoc />
    public override string AppDataPath { get; }

    /// <summary>Gets the directory a provider made for <paramref name="appName"/> stores its data in, which the test deletes when it is done.</summary>
    /// <param name="appName">The name the provider was made with.</param>
    public static string DirectoryOf(string appName) => Path.Combine(Path.GetTempPath(), "comlink-tests", appName);
}
