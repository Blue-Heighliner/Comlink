namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IKioskModeProvider"/> that enables kiosk mode when the <c>KIOSK_MODE</c> environment
/// variable is set to <c>"1"</c> or <c>"true"</c> (case-insensitive); disabled by default, matching the Engine default.
/// </summary>
public sealed class SampleKioskModeProvider : IKioskModeProvider
{
    /// <inheritdoc />
    public bool IsKioskMode
    {
        get
        {
            string? value = Environment.GetEnvironmentVariable("KIOSK_MODE");
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
