namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Debug override that supplies a fixed user name, bypassing the normal <c>State.json</c> lookup.</summary>
public interface IDebugUserOverride
{
    /// <summary>The overridden user name, or <see langword="null"/> if no override is active.</summary>
    string? UserName { get; }
}

/// <summary>Implements <see cref="IDebugUserOverride"/> using the user name from engine configuration.</summary>
internal sealed class DebugUserOverride : IDebugUserOverride
{
    private readonly EngineConfig _config;

    /// <summary>Initializes a new instance with the given engine configuration.</summary>
    /// <param name="config">Engine configuration providing the optional user name override.</param>
    public DebugUserOverride(EngineConfig config) => _config = config;

    /// <inheritdoc />
    public string? UserName => _config.UserName;
}
