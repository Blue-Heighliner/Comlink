namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="IDebugMode" />
internal sealed class DebugMode : IDebugMode
{
    /// <summary>The metadata key the package's build targets stamp into an assembly compiled with its <c>DEBUG</c> constant defined.</summary>
    internal static string MetadataKey { get; } = "BlueHeighliner.Comlink.Debug";

    /// <summary>Initializes the mode from the entry assembly.</summary>
    public DebugMode()
        : this(Assembly.GetEntryAssembly())
    {
    }

    /// <summary>Initializes the mode from <paramref name="assembly"/>.</summary>
    /// <param name="assembly">The assembly of the application, or <see langword="null"/> when there is none.</param>
    internal DebugMode(Assembly? assembly) => IsDebug = assembly?.GetCustomAttributes<AssemblyMetadataAttribute>().Any(metadata => metadata.Key == MetadataKey && metadata.Value == "true") ?? false;

    /// <inheritdoc />
    public bool IsDebug { get; }
}
