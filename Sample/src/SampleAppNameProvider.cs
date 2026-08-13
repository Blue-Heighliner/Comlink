namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IAppNameProvider"/> that reproduces the Engine default (entry assembly name) unless the
/// <c>APP_NAME</c> environment variable is set, letting an operator rename the app data folder without a rebuild.
/// </summary>
public sealed class SampleAppNameProvider : IAppNameProvider
{
    /// <inheritdoc />
    public string AppName =>
        Environment.GetEnvironmentVariable("APP_NAME")
        ?? Assembly.GetEntryAssembly()?.GetName().Name
        ?? "App";
}
