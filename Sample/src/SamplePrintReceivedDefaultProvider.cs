namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IPrintReceivedDefaultProvider"/> that honors <c>config.json</c>'s <c>PrintReceivedEnabled</c>
/// exactly like the Engine default, falling back to the <c>PRINT_RECEIVED_ENABLED</c> environment variable
/// (<c>"1"</c>/<c>"true"</c> to enable), then <see langword="false"/>.
/// </summary>
public sealed class SamplePrintReceivedDefaultProvider : IPrintReceivedDefaultProvider
{
    private readonly EngineConfig _config;

    /// <summary>Initializes a new <see cref="SamplePrintReceivedDefaultProvider"/> with the given engine configuration.</summary>
    /// <param name="config">Engine configuration providing the optional print-received-enabled setting.</param>
    public SamplePrintReceivedDefaultProvider(EngineConfig config) => _config = config;

    /// <inheritdoc />
    public bool DefaultEnabled => _config.PrintReceivedEnabled ?? ReadEnvFlag() ?? false;

    private static bool? ReadEnvFlag()
    {
        string? value = Environment.GetEnvironmentVariable("PRINT_RECEIVED_ENABLED");
        if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            return false;
        return null;
    }
}
