namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IDebugUserOverride"/> that honors <c>config.json</c>'s <c>UserName</c> exactly like the
/// Engine default, falling back to the <c>DEBUG_USER</c> environment variable when unset.
/// </summary>
/// <remarks>
/// Registering a host implementation of this interface takes the place of — rather than adds to — the
/// Engine's own default via the convention-registration `TryAddSingleton` pattern (see `Docs/Control.md`), even
/// though `UserService` consumes it as `IEnumerable&lt;IDebugUserOverride&gt;`. This implementation therefore
/// reproduces the config-driven behavior itself so registering it does not silently stop `config.json`'s
/// <c>UserName</c> field from working.
/// </remarks>
public sealed class SampleDebugUserOverride : IDebugUserOverride
{
    private readonly EngineConfig _config;

    /// <summary>Initializes a new <see cref="SampleDebugUserOverride"/> with the given engine configuration.</summary>
    /// <param name="config">Engine configuration providing the optional user name override.</param>
    public SampleDebugUserOverride(EngineConfig config) => _config = config;

    /// <inheritdoc />
    public string? UserName => _config.UserName ?? Environment.GetEnvironmentVariable("DEBUG_USER");
}
