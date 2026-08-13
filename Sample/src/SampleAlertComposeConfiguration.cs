namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IAlertComposeConfiguration"/> that honors <c>config.json</c>'s <c>ComposeAlertsEnabled</c>
/// exactly like the Engine default, falling back to the <c>COMPOSE_ALERTS_ENABLED</c> environment variable
/// (<c>"0"</c>/<c>"false"</c> to disable), then <see langword="true"/>.
/// </summary>
public sealed class SampleAlertComposeConfiguration : IAlertComposeConfiguration
{
    private readonly EngineConfig _config;

    /// <summary>Initializes a new <see cref="SampleAlertComposeConfiguration"/> with the given engine configuration.</summary>
    /// <param name="config">Engine configuration providing the optional compose-alerts setting.</param>
    public SampleAlertComposeConfiguration(EngineConfig config) => _config = config;

    /// <inheritdoc />
    public bool ComposeAlertsEnabled => _config.ComposeAlertsEnabled ?? ReadEnvFlag() ?? true;

    private static bool? ReadEnvFlag()
    {
        string? value = Environment.GetEnvironmentVariable("COMPOSE_ALERTS_ENABLED");
        if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            return true;
        return null;
    }
}
