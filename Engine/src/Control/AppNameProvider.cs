namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Provides the application name used for display and data storage.</summary>
public interface IAppNameProvider
{
    /// <summary>The application name.</summary>
    string AppName { get; }
}

/// <summary>Implements <see cref="IAppNameProvider"/> deriving the name from the entry assembly.</summary>
internal sealed class AppNameProvider : IAppNameProvider
{
    /// <inheritdoc />
    public string AppName => Assembly.GetEntryAssembly()?.GetName().Name ?? "App";
}
